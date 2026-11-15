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
import { Router } from '@angular/router';
import cytoscape, { Core, ElementDefinition } from 'cytoscape';

import { localNumber } from '../../core/i18n/local-format';
import { fullName, initials } from '../people/person';
import { PhotoLoader } from '../people/photo-loader.service';
import { compareChildren, orderRow, orderSiblings, siblingSets } from './child-order';
import { childLines, trunkLevels } from './couple-lines';
import { familyLayout } from './family-layout';
import { GraphPerson, LocalViewPerson } from './graph';
import { framedPng, imageColours, imageFileName, saveFile, scaleFor } from './graph-image';
import { ShownGraph, generationLabel } from './shown-graph';

/** The tree's spacing: within a family, between families, and between generations. */
const treeSpacing = { nodeSep: 24, groupSep: 48, rankSep: 56 };

/** The dots' spacing; they stand in rows like the tree, so children stay below their parents (D-055). */
const dotSpacing = { nodeSep: 8, groupSep: 24, rankSep: 40 };

/** The least space between two couples' bars at one height. */
const barMargin = 12;

/** The least way down before a trunk turns into its bar. */
const minTurn = 3;

const dotSize = 12;

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

/** The person's photo in the avatar's circle, in place of the initials (D-028). */
function photoAvatar(photoDataUrl: string): string {
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" width="${avatarSize}" height="${avatarSize}">` +
    `<clipPath id="c"><circle cx="12" cy="12" r="12"/></clipPath>` +
    `<image href="${photoDataUrl}" width="${avatarSize}" height="${avatarSize}" ` +
    `preserveAspectRatio="xMidYMid slice" clip-path="url(#c)"/></svg>`;
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

/** Who you can add to a person from the diagram's menu (D-032). */
export type AddRelation = 'parent' | 'child' | 'spouse';

/** A request from the menu to add relatives of a person. */
export interface AddRelativeRequest {
  person: GraphPerson;
  relation: AddRelation;
}

/** The person under the pointer, placed relative to the canvas. */
interface Hovered {
  row: LocalViewPerson;
  x: number;
  y: number;
}

/**
 * The tree diagram of the shown people (PRD 08, D-024): parents above children, drawn by
 * Cytoscape on a canvas. Both the tree and the home page's dots use the family layout, which keeps
 * couples side by side and children below their parents, each generation on its row (D-055). Spouses share a row, joined by a double line
 * that is dotted once the marriage ended (D-031). A child of a current or former couple has one line, from the
 * middle of the couple's line (D-054). Adoptive links are dashed and people with relatives
 * not shown have a double border. Each person has their photo or a coloured initial avatar and hovering shows their
 * details. The dots mode (the home page) draws each person as a small coloured dot, with no legend
 * or details; hovering shows only their name. With `addMenu`, right-clicking or pressing and
 * holding a person opens a menu to add their parents, children or spouse (D-032), to open their
 * Bansawali (D-043), and to delete the profile when you maintain it or are an administrator (D-034); a deleted person has no menu,
 * because nobody can be linked to them and they cannot be deleted again. The canvas is not keyboard or screen
 * reader accessible, so the page always offers the same people as links or text next to it, and
 * the person page the same ways to add relatives and delete the profile (AC-09.2).
 */
@Component({
  selector: 'app-graph-diagram',
  templateUrl: './graph-diagram.html',
  styleUrl: './graph-diagram.css',
})
export class GraphDiagram {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly photos = inject(PhotoLoader);
  private readonly router = inject(Router);

  readonly graph = input.required<ShownGraph>();
  readonly selectedId = input<string | null>(null);
  /** Only dots and lines, with the name alone on hover: no legend, centre or "has more" borders. */
  readonly dots = input(false);
  /** Offers the menu to add relatives; never in dots mode. */
  readonly addMenu = input(false);
  /** The viewer is an administrator, so the menu offers to delete anyone (PRD 05). */
  readonly administrator = input(false);
  readonly selected = output<string>();
  readonly addRelative = output<AddRelativeRequest>();
  readonly deletePerson = output<GraphPerson>();

  protected readonly fullName = fullName;
  protected readonly generationLabel = generationLabel;
  protected readonly hovered = signal<Hovered | null>(null);
  /** The person whose menu is open, placed like the tooltip. */
  protected readonly menu = signal<Hovered | null>(null);

  protected readonly summary = computed(() => {
    const count = this.graph().people.length;
    const diagram =
      count === 1
        ? $localize`:@@diagram.summaryOne:Family diagram of 1 person.`
        : $localize`:@@diagram.summaryMany:Family diagram of ${localNumber(count)}:count: people.`;
    return this.dots()
      ? diagram
      : $localize`:@@diagram.summaryWithList:${diagram}:diagram: The list of people below has the same relationships.`;
  });

  private readonly canvas = viewChild.required<ElementRef<HTMLElement>>('canvas');
  private readonly menuElement = viewChild<ElementRef<HTMLElement>>('menu');
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
      cy.on('tap', 'node.person', (event) => {
        this.hovered.set(null);
        this.menu.set(null);
        this.selected.emit(event.target.id());
      });
      // A right click, or a long press on a touch screen.
      cy.on('cxttap taphold', 'node.person', (event) => this.openMenu(event.target));
      cy.on('tap cxttap', (event) => {
        if (event.target === cy) {
          this.menu.set(null);
        }
      });
      // The browser's own menu would cover the diagram's.
      container.addEventListener('contextmenu', (event) => {
        if (this.addMenu() && !this.dots()) {
          event.preventDefault();
        }
      });
      cy.on('mouseover', 'node.person', (event) => {
        const row = this.graph().people.find((row) => row.person.id === event.target.id());
        const position = event.target.renderedPosition();
        const radius = event.target.renderedOuterHeight() / 2;
        container.style.cursor = 'pointer';
        this.hovered.set(row ? { row, x: position.x, y: position.y - radius } : null);
      });
      cy.on('mouseout', 'node.person', () => {
        container.style.cursor = '';
        this.hovered.set(null);
      });
      // The tooltip would drift from its node while panning or zooming.
      cy.on('viewport', () => {
        this.hovered.set(null);
        this.menu.set(null);
      });
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
      this.menu.set(null);
      cy.batch(() => {
        cy.elements().remove();
        cy.style(this.style(dots));
        cy.add(this.elements(this.graph()));
      });
      if (dots) {
        cy.layout(this.dotsLayout(cy, this.graph())).run();
        this.placeBars(cy, dotSize, dotSpacing.rankSep);
      } else {
        this.treeLayout(cy, this.graph());
      }
      if (!dots) {
        this.showPhotos(cy, this.graph());
      }
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
      cy.nodes('.person').removeClass('selected');
      if (id) {
        cy.getElementById(id).addClass('selected');
      }
    });
  }

  protected fit(): void {
    this.cy()?.fit(undefined, 24);
  }

  /** The image is being made; the button waits until it is saved. */
  protected readonly saving = signal(false);
  /** Making the image failed, for example in a browser that cannot draw one this large. */
  protected readonly saveFailed = signal(false);

  /**
   * Saves the whole tree shown, not just the part in view, as a sharp PNG (D-056). It is drawn in
   * the light theme's colours on white, so it reads the same printed or shared; it holds only what
   * the diagram shows, so placeholders stay placeholders.
   */
  protected async saveImage(): Promise<void> {
    const cy = this.cy();
    if (!cy || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.saveFailed.set(false);
    try {
      const box = cy.elements().boundingBox();
      const scale = scaleFor(box.w, box.h);
      // Cytoscape draws the image at once, so the screen never shows the light colours.
      cy.style(this.style(false, imageColours));
      let drawn: Blob;
      try {
        drawn = cy.png({ output: 'blob', full: true, scale, bg: imageColours.surface });
      } finally {
        cy.style(this.style(false));
      }
      const graph = this.graph();
      const centre = graph.people.find((row) => row.person.id === graph.centreId)?.person;
      saveFile(await framedPng(drawn, scale), imageFileName(centre ? fullName(centre) : ''));
    } catch {
      this.saveFailed.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  protected choose(relation: AddRelation): void {
    const menu = this.menu();
    this.menu.set(null);
    if (menu) {
      this.addRelative.emit({ person: menu.row.person, relation });
    }
  }

  /** The server decides; this only leaves the item out for people you could not delete. */
  protected menuLabel(person: GraphPerson): string {
    return $localize`:@@diagram.actionsFor:Actions for ${fullName(person)}:name:`;
  }

  protected canDelete(person: GraphPerson): boolean {
    return person.maintainedByYou || this.administrator();
  }

  protected chooseDelete(): void {
    const menu = this.menu();
    this.menu.set(null);
    if (menu) {
      this.deletePerson.emit(menu.row.person);
    }
  }

  /** Opens the person's Bansawali (D-043). */
  protected showBansawali(): void {
    const menu = this.menu();
    this.menu.set(null);
    if (menu) {
      void this.router.navigate(['/people', menu.row.person.id, 'bansawali']);
    }
  }

  protected closeMenu(): void {
    this.menu.set(null);
  }

  private openMenu(node: cytoscape.NodeSingular): void {
    const row = this.graph().people.find((row) => row.person.id === node.id());
    if (!this.addMenu() || this.dots() || !row || row.person.placeholder === 'Deleted') {
      return;
    }
    const position = node.renderedPosition();
    const radius = node.renderedOuterHeight() / 2;
    this.hovered.set(null);
    this.menu.set({ row, x: position.x, y: position.y + radius });
    // The menu renders on the next change detection; then its first item takes the focus.
    setTimeout(() => this.menuElement()?.nativeElement.querySelector('button')?.focus());
  }

  /** Swaps initials for photos as they load; the home page's dots never show photos (D-028). */
  private showPhotos(cy: Core, graph: ShownGraph): void {
    for (const { person } of graph.people) {
      void this.photos.dataUrl(person).then((photo) => {
        const node = cy.getElementById(person.id);
        if (photo && !cy.destroyed() && node.nonempty()) {
          node.data('avatar', photoAvatar(photo));
        }
      });
    }
  }

  /**
   * The tree diagram by the family layout, one row per generation (D-053). Then each row puts
   * brothers and sisters in birth order where they stand side by side (D-039).
   */
  private treeLayout(cy: cytoscape.Core, graph: ShownGraph): void {
    const width = new Map(cy.nodes('.person').map((node) => [node.id(), node.width()] as const));
    const positions = familyLayout(
      orderSiblings(graph.people, graph.links).map((row) => ({
        id: row.person.id,
        width: width.get(row.person.id) ?? avatarSize,
      })),
      graph.links,
      { height: avatarSize + gap * 2, ...treeSpacing, settle: false },
      graph.marriages.map((marriage) => ({
        spouseId: marriage.firstSpouseId,
        otherSpouseId: marriage.secondSpouseId,
      })),
    );
    cy.layout({
      name: 'preset',
      positions: Object.fromEntries(this.withJunctions(cy, positions)),
      padding: 24,
    }).run();

    const sets = [...siblingSets(graph.people, graph.links).values()].map((rows) =>
      rows.map((row) => row.person.id),
    );
    const partners = new Map<string, Set<string>>();
    for (const marriage of graph.marriages) {
      for (const [a, b] of [
        [marriage.firstSpouseId, marriage.secondSpouseId],
        [marriage.secondSpouseId, marriage.firstSpouseId],
      ]) {
        partners.set(a, (partners.get(a) ?? new Set()).add(b));
      }
    }
    const rows = new Map<number, { id: string; x: number; width: number }[]>();
    cy.nodes('.person').forEach((node) => {
      const y = Math.round(node.position('y'));
      rows.set(y, [
        ...(rows.get(y) ?? []),
        { id: node.id(), x: node.position('x'), width: node.width() },
      ]);
    });
    cy.batch(() => {
      for (const row of rows.values()) {
        for (const [id, x] of orderRow(row, sets, partners)) {
          const node = cy.getElementById(id);
          node.position({ x, y: node.position('y') });
        }
      }
      const moved = new Map(cy.nodes('.person').map((node) => [node.id(), node.position()]));
      for (const [id, point] of this.withJunctions(cy, moved)) {
        if (!moved.has(id)) {
          cy.getElementById(id).position(point);
        }
      }
      this.placeBars(cy, avatarSize + gap * 2, treeSpacing.rankSep);
    });
  }

  /**
   * How far below its couple or lone parent each bar turns towards the children (D-054, D-055):
   * between the two rows, with bars that would run over each other at different heights.
   */
  private placeBars(cy: cytoscape.Core, height: number, rankSep: number): void {
    const rows = new Map<number, cytoscape.NodeSingular[]>();
    cy.edges('.trunk')
      .sources()
      .forEach((source) => {
        const y = Math.round(source.position('y'));
        rows.set(y, [...(rows.get(y) ?? []), source]);
      });
    const half = height / 2;
    for (const sources of rows.values()) {
      const levels = trunkLevels(
        sources.map((source) => {
          const ends = [
            source.position('x'),
            ...source
              .outgoers('edge.trunk')
              .targets()
              .map((child) => child.position('x')),
          ];
          return { id: source.id(), from: Math.min(...ends), to: Math.max(...ends) };
        }),
        barMargin,
      );
      const heights = Math.max(...levels.values()) + 1;
      for (const source of sources) {
        // A turn is measured from the parent's lower side, or from the junction's middle.
        const turn =
          (source.hasClass('junction') ? half : 0) +
          (rankSep * (levels.get(source.id())! + 1)) / (heights + 1);
        source.outgoers('edge.trunk').data('turn', Math.round(turn));
      }
    }
  }

  /**
   * The positions with each couple's junction in the middle of their marriage line (D-054): half
   * way between the two people's facing sides, so it sits on the line's middle even when one name
   * box is wider than the other.
   */
  private withJunctions(
    cy: cytoscape.Core,
    positions: ReadonlyMap<string, { x: number; y: number }>,
  ): Map<string, { x: number; y: number }> {
    const all = new Map(positions);
    cy.nodes('.junction').forEach((junction) => {
      const [first, second] = junction.data('spouses') as [string, string];
      const from = positions.get(first);
      const to = positions.get(second);
      if (!from || !to) {
        return;
      }
      const length = Math.hypot(to.x - from.x, to.y - from.y);
      const start = cy.getElementById(first).width() / 2;
      const end = cy.getElementById(second).width() / 2;
      // Overlapping people have no visible line; the middle of their centres will do.
      const along = length > start + end ? (start + (length - end)) / 2 / length : 0.5;
      all.set(junction.id(), {
        x: from.x + (to.x - from.x) * along,
        y: from.y + (to.y - from.y) * along,
      });
    });
    return all;
  }

  /** The dots in rows by generation, each family's children below their parents (D-055). */
  private dotsLayout(cy: cytoscape.Core, graph: ShownGraph): cytoscape.PresetLayoutOptions {
    // Brothers and sisters stand in birth order (D-039).
    const positions = familyLayout(
      orderSiblings(graph.people, graph.links).map((row) => ({
        id: row.person.id,
        width: dotSize,
      })),
      graph.links,
      { height: dotSize, ...dotSpacing, settle: false },
      graph.marriages.map((marriage) => ({
        spouseId: marriage.firstSpouseId,
        otherSpouseId: marriage.secondSpouseId,
      })),
    );
    return {
      name: 'preset',
      positions: Object.fromEntries(this.withJunctions(cy, positions)),
      padding: 16,
    };
  }

  /**
   * People and each parent's links in birth order (D-039), so brothers and sisters start eldest on
   * the left. A child of a current or former couple gets one line from the couple's junction, an unseen
   * point in the middle of their marriage line, in place of a line from each parent (D-054).
   */
  private elements(graph: ShownGraph): ElementDefinition[] {
    const ids = new Set(graph.people.map((row) => row.person.id));
    const people = new Map(graph.people.map((row) => [row.person.id, row.person]));
    const links = graph.links
      .filter((link) => ids.has(link.parentId) && ids.has(link.childId))
      .map((link) => ({ person: people.get(link.childId)!, link }))
      .sort(compareChildren)
      .map(({ link }) => link);
    const measure = this.textWidth();
    const lines = childLines(ids, links, graph.marriages);
    const marriages = new Map(graph.marriages.map((marriage) => [marriage.id, marriage]));
    const junction = (marriageId: string) => `junction:${marriageId}`;
    return [
      ...orderSiblings(graph.people, graph.links).map((row) => ({
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
          'person',
          row.person.id === graph.centreId ? 'centre' : '',
          row.hasMore ? 'has-more' : '',
        ].join(' '),
      })),
      ...[...new Set(lines.couples.map((line) => line.marriageId))].map((marriageId) => ({
        group: 'nodes' as const,
        data: {
          id: junction(marriageId),
          spouses: [
            marriages.get(marriageId)!.firstSpouseId,
            marriages.get(marriageId)!.secondSpouseId,
          ],
        },
        classes: 'junction',
      })),
      ...lines.lone.map((link) => ({
        group: 'edges' as const,
        data: { id: link.id, source: link.parentId, target: link.childId, turn: 0 },
        classes: ['trunk', link.type === 'Adoptive' ? 'adoptive' : ''].join(' '),
      })),
      ...lines.single.map((link) => ({
        group: 'edges' as const,
        data: { id: link.id, source: link.parentId, target: link.childId },
        classes: link.type === 'Adoptive' ? 'adoptive' : '',
      })),
      ...lines.couples.map((line) => ({
        group: 'edges' as const,
        data: {
          id: line.links[0].id,
          source: junction(line.marriageId),
          target: line.childId,
          turn: 0,
        },
        classes: ['trunk', line.links[0].type === 'Adoptive' ? 'adoptive' : ''].join(' '),
      })),
      ...graph.marriages
        .filter((marriage) => ids.has(marriage.firstSpouseId) && ids.has(marriage.secondSpouseId))
        .map((marriage) => ({
          group: 'edges' as const,
          data: {
            id: marriage.id,
            source: marriage.firstSpouseId,
            target: marriage.secondSpouseId,
          },
          classes: marriage.isFormer ? 'marriage former' : 'marriage',
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

  /**
   * Colours come from the page's CSS custom properties, so the diagram follows the theme; the
   * saved image passes the light theme's colours instead (D-056).
   */
  private style(
    dots: boolean,
    colours: Partial<typeof imageColours> = {},
  ): cytoscape.StylesheetJson {
    const css = getComputedStyle(this.host.nativeElement);
    const colour = (name: string, fallback: string) =>
      css.getPropertyValue(name).trim() || fallback;
    const text = colours.text ?? colour('--text', '#1f1f24');
    const muted = colours.muted ?? colour('--muted', '#5b5b66');
    const accent = colours.accent ?? colour('--accent', '#2f5bd3');
    const background = colours.surface ?? colour('--surface', '#ffffff');
    const nodes: cytoscape.StylesheetJson = dots
      ? [
          {
            selector: 'node.person',
            style: {
              shape: 'ellipse',
              width: dotSize,
              height: dotSize,
              'background-color': 'data(colour)',
            },
          },
        ]
      : [
          {
            selector: 'node.person',
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
      // Where a couple's children's lines start: a point nobody sees, hovers or selects.
      {
        selector: 'node.junction',
        style: { width: 1, height: 1, opacity: 0, events: 'no' },
      },
      {
        selector: 'node.selected',
        style: { 'border-color': accent, 'underlay-color': accent, 'underlay-opacity': 0.15 },
      },
      {
        selector: 'edge',
        style: {
          width: dots ? 1 : 1.5,
          'line-color': muted,
          // Each link outside a marriage gets its own straight line, so parents stay unambiguous.
          'curve-style': 'bezier',
        },
      },
      // The children of a couple, or of a lone parent, share one line down to a bar, with a drop to
      // each child.
      {
        selector: 'edge.trunk',
        style: {
          'curve-style': 'taxi',
          'taxi-direction': 'downward',
          'taxi-turn': 'data(turn)',
          'taxi-turn-min-distance': minTurn,
        },
      },
      {
        selector: 'edge.adoptive',
        style: { 'line-style': 'dashed', 'line-dash-pattern': dots ? [3, 3] : [6, 4] },
      },
      // Spouses are joined by a double line in the accent colour, dotted once the marriage ended.
      {
        selector: 'edge.marriage',
        style: {
          'line-color': accent,
          'line-style': 'double',
          width: dots ? 2 : 4,
        },
      },
      {
        selector: 'edge.marriage.former',
        style: { 'line-style': 'dotted', width: dots ? 1 : 2 },
      },
    ];
  }
}
