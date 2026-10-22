import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import cytoscape, { Core, ElementDefinition } from 'cytoscape';
import dagre, { DagreLayoutOptions } from 'cytoscape-dagre';

import { fullName, initials } from '../people/person';
import { LocalViewPerson } from './graph';
import { ShownGraph, generationLabel } from './shown-graph';

cytoscape.use(dagre);

const layout: DagreLayoutOptions = {
  name: 'dagre',
  rankDir: 'TB',
  nodeSep: 24,
  rankSep: 56,
  padding: 24,
};

const dotsLayout: DagreLayoutOptions = { ...layout, nodeSep: 10, rankSep: 28, padding: 16 };

/** Avatar and dot fills, dark enough for white initials. */
const avatarColours = [
  '#c2410c',
  '#b45309',
  '#4d7c0f',
  '#047857',
  '#0e7490',
  '#1d4ed8',
  '#6d28d9',
  '#be185d',
];

/** The same person always gets the same colour. */
function avatarColour(personId: string): string {
  let hash = 0;
  for (const char of personId) {
    hash = (hash * 31 + char.charCodeAt(0)) | 0;
  }
  return avatarColours[Math.abs(hash) % avatarColours.length];
}

const fontSize = 13;
const avatarSize = 24;
/** Space left of the avatar, between it and the name, and right of the name. */
const gap = 8;
const maxLabelWidth = 160;

/** A coloured circle with the initials, drawn inside a name box. */
function avatarImage(personInitials: string, colour: string): string {
  const escaped = personInitials.replace(/[<>&'"]/g, (char) => `&#${char.charCodeAt(0)};`);
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" width="${avatarSize}" height="${avatarSize}">` +
    `<circle cx="12" cy="12" r="12" fill="${colour}"/>` +
    `<text x="12" y="12" dy="0.35em" text-anchor="middle" fill="#ffffff" ` +
    `font-family="system-ui, sans-serif" font-size="10" font-weight="bold">${escaped}</text></svg>`;
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

/** The person under the pointer, placed relative to the canvas. */
interface Hovered {
  row: LocalViewPerson;
  x: number;
  y: number;
}

/**
 * The tree diagram of the shown people (PRD 08, D-024): parents above children, laid out by
 * dagre and drawn by Cytoscape on a canvas. Adoptive links are dashed and people with relatives
 * not shown have a double border. Each person has a coloured initial avatar and hovering shows their
 * details. The dots mode (the home page) draws each person as a small coloured dot, with no legend
 * or details; hovering shows only their name. The canvas is not keyboard or screen reader accessible, so the page always
 * offers the same people as links or text next to it (AC-09.2).
 */
@Component({
  selector: 'app-graph-diagram',
  templateUrl: './graph-diagram.html',
  styleUrl: './graph-diagram.css',
})
export class GraphDiagram {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly graph = input.required<ShownGraph>();
  readonly selectedId = input<string | null>(null);
  /** Only dots and lines, with the name alone on hover: no legend, centre or "has more" borders. */
  readonly dots = input(false);
  readonly selected = output<string>();

  protected readonly fullName = fullName;
  protected readonly generationLabel = generationLabel;
  protected readonly hovered = signal<Hovered | null>(null);

  protected readonly summary = computed(() => {
    const count = this.graph().people.length;
    const diagram = `Family diagram of ${count} ${count === 1 ? 'person' : 'people'}.`;
    return this.dots()
      ? diagram
      : `${diagram} The list of people below has the same relationships.`;
  });

  private readonly canvas = viewChild.required<ElementRef<HTMLElement>>('canvas');
  private readonly cy = signal<Core | null>(null);

  constructor() {
    afterNextRender(() => {
      const container = this.canvas().nativeElement;
      const cy = cytoscape({
        container,
        minZoom: 0.2,
        maxZoom: 2.5,
        boxSelectionEnabled: false,
        autoungrabify: true,
      });
      cy.on('tap', 'node', (event) => {
        this.hovered.set(null);
        this.selected.emit(event.target.id());
      });
      cy.on('mouseover', 'node', (event) => {
        const row = this.graph().people.find((row) => row.person.id === event.target.id());
        const position = event.target.renderedPosition();
        const radius = event.target.renderedOuterHeight() / 2;
        container.style.cursor = 'pointer';
        this.hovered.set(row ? { row, x: position.x, y: position.y - radius } : null);
      });
      cy.on('mouseout', 'node', () => {
        container.style.cursor = '';
        this.hovered.set(null);
      });
      // The tooltip would drift from its node while panning or zooming.
      cy.on('viewport', () => this.hovered.set(null));
      this.cy.set(cy);
    });
    inject(DestroyRef).onDestroy(() => this.cy()?.destroy());

    // Redraws when people are added; the layout is cheap at the sizes a view allows.
    effect(() => {
      const cy = this.cy();
      if (!cy) {
        return;
      }
      const dots = this.dots();
      this.hovered.set(null);
      cy.batch(() => {
        cy.elements().remove();
        cy.style(this.style(dots));
        cy.add(this.elements(this.graph()));
      });
      cy.layout(dots ? dotsLayout : layout).run();
      // A small family would otherwise be zoomed in until the dots are no longer small.
      if (dots && cy.zoom() > 1) {
        cy.zoom(1);
        cy.center();
      }
    });

    effect(() => {
      const cy = this.cy();
      const id = this.selectedId();
      if (!cy) {
        return;
      }
      // Reads the graph so a redraw keeps the selection.
      this.graph();
      cy.nodes().removeClass('selected');
      if (id) {
        cy.getElementById(id).addClass('selected');
      }
    });
  }

  protected fit(): void {
    this.cy()?.fit(undefined, 24);
  }

  private elements(graph: ShownGraph): ElementDefinition[] {
    const ids = new Set(graph.people.map((row) => row.person.id));
    const measure = this.textWidth();
    return [
      ...graph.people.map((row) => ({
        group: 'nodes' as const,
        data: {
          id: row.person.id,
          label: fullName(row.person),
          initials: initials(row.person),
          colour: avatarColour(row.person.id),
          avatar: avatarImage(initials(row.person), avatarColour(row.person.id)),
          width: gap * 3 + avatarSize + Math.min(maxLabelWidth, measure(fullName(row.person))),
        },
        classes: [
          row.person.id === graph.centreId ? 'centre' : '',
          row.hasMore ? 'has-more' : '',
        ].join(' '),
      })),
      ...graph.links
        .filter((link) => ids.has(link.parentId) && ids.has(link.childId))
        .map((link) => ({
          group: 'edges' as const,
          data: { id: link.id, source: link.parentId, target: link.childId },
          classes: link.type === 'Adoptive' ? 'adoptive' : '',
        })),
    ];
  }

  /** Measures a name in the diagram's font, to size its box around the avatar and the name. */
  private textWidth(): (text: string) => number {
    const context = document.createElement('canvas').getContext('2d');
    if (!context) {
      return (text) => text.length * fontSize * 0.6;
    }
    context.font = `${fontSize}px ${this.fontFamily()}`;
    return (text) => Math.ceil(context.measureText(text).width);
  }

  private fontFamily(): string {
    return getComputedStyle(this.host.nativeElement).fontFamily || 'system-ui, sans-serif';
  }

  /** Colours come from the page's CSS custom properties, so the diagram follows the theme. */
  private style(dots: boolean): cytoscape.StylesheetJson {
    const css = getComputedStyle(this.host.nativeElement);
    const colour = (name: string, fallback: string) =>
      css.getPropertyValue(name).trim() || fallback;
    const text = colour('--text', '#1f1f24');
    const muted = colour('--muted', '#5b5b66');
    const accent = colour('--accent', '#2f5bd3');
    const background = colour('--surface', '#ffffff');
    const nodes: cytoscape.StylesheetJson = dots
      ? [
          {
            selector: 'node',
            style: { shape: 'ellipse', width: 12, height: 12, 'background-color': 'data(colour)' },
          },
        ]
      : [
          {
            selector: 'node',
            style: {
              shape: 'round-rectangle',
              label: 'data(label)',
              width: 'data(width)',
              height: avatarSize + gap * 2,
              'text-valign': 'center',
              'text-halign': 'center',
              // Moves the name right of the avatar; names too long for the box end in "…".
              'text-margin-x': (avatarSize + gap) / 2,
              'text-wrap': 'ellipsis',
              'text-max-width': `${maxLabelWidth}px`,
              'font-size': `${fontSize}px`,
              'font-family': this.fontFamily(),
              'background-image': 'data(avatar)',
              'background-width': `${avatarSize}px`,
              'background-height': `${avatarSize}px`,
              'background-fit': 'none',
              'background-position-x': `${gap}px`,
              'background-position-y': '50%',
              'background-clip': 'none',
              color: text,
              'background-color': background,
              'border-width': 1,
              'border-color': muted,
            },
          },
          { selector: 'node.has-more', style: { 'border-width': 4, 'border-style': 'double' } },
          { selector: 'node.centre', style: { 'border-color': accent, 'font-weight': 'bold' } },
        ];
    return [
      ...nodes,
      {
        selector: 'node.selected',
        style: { 'border-color': accent, 'underlay-color': accent, 'underlay-opacity': 0.15 },
      },
      {
        selector: 'edge',
        style: {
          width: dots ? 1 : 1.5,
          'line-color': muted,
          // Taxi edges share segments, which blurs who is whose parent; each link gets its own line.
          'curve-style': 'bezier',
        },
      },
      {
        selector: 'edge.adoptive',
        style: { 'line-style': 'dashed', 'line-dash-pattern': dots ? [3, 3] : [6, 4] },
      },
    ];
  }
}
