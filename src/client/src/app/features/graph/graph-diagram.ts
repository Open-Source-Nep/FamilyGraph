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

import { fullName } from '../people/person';
import { ShownGraph } from './shown-graph';

cytoscape.use(dagre);

const layout: DagreLayoutOptions = {
  name: 'dagre',
  rankDir: 'TB',
  nodeSep: 24,
  rankSep: 56,
  padding: 24,
};

/**
 * The tree diagram of the shown people (PRD 08, D-024): parents above children, laid out by
 * dagre and drawn by Cytoscape on a canvas. Adoptive links are dashed and people with relatives
 * not shown have a double border. The canvas is not keyboard or screen reader accessible, so the
 * page always shows the text view next to it (AC-09.2).
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
  readonly selected = output<string>();

  protected readonly summary = computed(() => {
    const count = this.graph().people.length;
    return `Family diagram of ${count} ${count === 1 ? 'person' : 'people'}. The list of people below has the same relationships.`;
  });

  private readonly canvas = viewChild.required<ElementRef<HTMLElement>>('canvas');
  private readonly cy = signal<Core | null>(null);

  constructor() {
    afterNextRender(() => {
      const cy = cytoscape({
        container: this.canvas().nativeElement,
        style: this.style(),
        minZoom: 0.2,
        maxZoom: 2.5,
        boxSelectionEnabled: false,
        autoungrabify: true,
      });
      cy.on('tap', 'node', (event) => this.selected.emit(event.target.id()));
      this.cy.set(cy);
    });
    inject(DestroyRef).onDestroy(() => this.cy()?.destroy());

    // Redraws when people are added; the layout is cheap at the sizes a view allows.
    effect(() => {
      const cy = this.cy();
      if (!cy) {
        return;
      }
      cy.batch(() => {
        cy.elements().remove();
        cy.add(this.elements(this.graph()));
      });
      cy.layout(layout).run();
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
    return [
      ...graph.people.map((row) => ({
        group: 'nodes' as const,
        data: { id: row.person.id, label: fullName(row.person) },
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

  /** Colours come from the page's CSS custom properties, so the diagram follows the theme. */
  private style(): cytoscape.StylesheetJson {
    const css = getComputedStyle(this.host.nativeElement);
    const colour = (name: string, fallback: string) =>
      css.getPropertyValue(name).trim() || fallback;
    const text = colour('--text', '#1f1f24');
    const muted = colour('--muted', '#5b5b66');
    const accent = colour('--accent', '#2f5bd3');
    const background = colour('--surface', '#ffffff');
    return [
      {
        selector: 'node',
        style: {
          shape: 'round-rectangle',
          label: 'data(label)',
          width: 'label',
          height: 'label',
          padding: '10px',
          'text-valign': 'center',
          'text-halign': 'center',
          'text-wrap': 'wrap',
          'text-max-width': '140px',
          'font-size': '13px',
          color: text,
          'background-color': background,
          'border-width': 1,
          'border-color': muted,
        },
      },
      { selector: 'node.has-more', style: { 'border-width': 4, 'border-style': 'double' } },
      { selector: 'node.centre', style: { 'border-color': accent, 'font-weight': 'bold' } },
      {
        selector: 'node.selected',
        style: { 'border-color': accent, 'underlay-color': accent, 'underlay-opacity': 0.15 },
      },
      {
        selector: 'edge',
        style: {
          width: 1.5,
          'line-color': muted,
          // Taxi edges share segments, which blurs who is whose parent; each link gets its own line.
          'curve-style': 'bezier',
        },
      },
      { selector: 'edge.adoptive', style: { 'line-style': 'dashed', 'line-dash-pattern': [6, 4] } },
    ];
  }
}
