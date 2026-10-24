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

import { PeopleService } from '../people/people.service';
import { PersonDetails, PersonName, fullName } from '../people/person';
import { PersonNameForm } from '../people/person-name-form';
import { problemMessage } from '../people/problem';
import { MarriagesService } from '../relationships/marriages.service';
import { ParentChildType } from '../relationships/relationship';
import { RelationshipsService } from '../relationships/relationships.service';
import { GraphService } from './graph.service';
import { GraphPerson } from './graph';
import { AddRelation } from './graph-diagram';

/** Someone you can pick, by the name you see. */
interface Candidate {
  id: string;
  name: string;
  maintainedByYou: boolean;
}

/** What happened to one of the people you added together. Each is linked on its own. */
type AddOutcome =
  { name: string; state: 'active' | 'pending' } | { name: string; state: 'failed'; reason: string };

/** How the dialog ended: whether anything was saved, and what to tell you about it. */
export interface AddRelativesResult {
  changed: boolean;
  message: string | null;
}

/** Searches start at this many characters, and wait this long after the last key. */
const minSearchLength = 2;
const searchDelayMs = 300;

/** Every word of the text appears in the first, middle or last name, ignoring case, like the server's search. */
export function matchesName(name: PersonName, text: string): boolean {
  const parts = [name.firstName, name.middleName, name.lastName]
    .filter((part): part is string => Boolean(part))
    .map((part) => part.toLocaleLowerCase());
  return text
    .toLocaleLowerCase()
    .split(/\s+/)
    .filter(Boolean)
    .every((word) => parts.some((part) => part.includes(word)));
}

/**
 * Adds parents, children or a spouse to a person from the graph page's diagram (D-032). The list
 * starts with your connections: your own profiles and the people you can see by name who are
 * linked to them. Typing also searches every Public profile, so separate families can be joined.
 * On a profile someone else maintains, you can only pick your own profiles, because you must
 * maintain one of the two people (PRD 03). "Add a new person" creates a profile you maintain and
 * selects it, as on the person page (#66). The server checks consent, cycles and duplicates; a side
 * maintained by someone else waits for their approval.
 */
@Component({
  selector: 'app-add-relatives',
  imports: [PersonNameForm],
  templateUrl: './add-relatives.html',
  styleUrl: './add-relatives.css',
})
export class AddRelatives {
  private readonly people = inject(PeopleService);
  private readonly graph = inject(GraphService);
  private readonly relationships = inject(RelationshipsService);
  private readonly marriages = inject(MarriagesService);

  readonly person = input.required<GraphPerson>();
  readonly relation = input.required<AddRelation>();
  readonly closed = output<AddRelativesResult>();

  protected readonly fullName = fullName;
  protected readonly name = computed(() => fullName(this.person()));
  protected readonly title = computed(() => {
    const name = this.name();
    return {
      parent: `Add parents of ${name}`,
      child: `Add children of ${name}`,
      spouse: `Add a spouse of ${name}`,
    }[this.relation()];
  });
  /** Your profiles, and the people linked to them when the person is yours; null while loading. */
  protected readonly connections = signal<Candidate[] | null>(null);
  protected readonly connectionsTruncated = signal(false);
  protected readonly query = signal('');
  /** Server matches for the current query, beyond your connections. */
  protected readonly found = signal<Candidate[]>([]);
  protected readonly foundTruncated = signal(false);
  protected readonly searching = signal(false);
  /** The people to add, in the order they were picked, kept while the list is filtered. */
  protected readonly selected = signal<Candidate[]>([]);
  protected readonly type = signal<ParentChildType>('Biological');
  protected readonly creatingPerson = signal(false);
  protected readonly selectionMissing = signal(false);
  protected readonly outcomes = signal<AddOutcome[]>([]);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Only when the person is yours can the other one be anybody's (PRD 03). */
  protected readonly searchesEveryone = computed(() => this.person().maintainedByYou);
  protected readonly single = computed(() => this.relation() === 'spouse');
  protected readonly options = computed(() => {
    const text = this.query().trim();
    const local = (this.connections() ?? []).filter(
      (candidate) => !text || matchesName(this.nameParts(candidate), text),
    );
    const ids = new Set(local.map((candidate) => candidate.id));
    return [...local, ...this.found().filter((candidate) => !ids.has(candidate.id))];
  });

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  /** Name parts by ID, for filtering your connections as you type. */
  private readonly names = new Map<string, PersonName>();
  private changed = false;
  private finished = false;
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    afterNextRender(() => {
      const dialog = this.dialog().nativeElement;
      // jsdom has no showModal, so fall back to a non-modal dialog there.
      if (typeof dialog.showModal === 'function') {
        dialog.showModal();
      } else {
        dialog.setAttribute('open', '');
      }
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.searchTimer));
    effect(() => void this.loadConnections(this.person()));
  }

  protected setQuery(text: string): void {
    this.query.set(text);
    clearTimeout(this.searchTimer);
    const trimmed = text.trim();
    if (!this.searchesEveryone() || trimmed.length < minSearchLength) {
      this.found.set([]);
      this.foundTruncated.set(false);
      this.searching.set(false);
      return;
    }
    this.searching.set(true);
    this.searchTimer = setTimeout(() => void this.search(trimmed), searchDelayMs);
  }

  protected isSelected(id: string): boolean {
    return this.selected().some((candidate) => candidate.id === id);
  }

  protected toggle(candidate: Candidate, checked: boolean): void {
    if (this.single()) {
      this.selected.set(checked ? [candidate] : []);
    } else {
      this.selected.update((list) =>
        checked
          ? this.isSelected(candidate.id)
            ? list
            : [...list, candidate]
          : list.filter((other) => other.id !== candidate.id),
      );
    }
    if (checked) {
      this.selectionMissing.set(false);
    }
  }

  protected selectedNames(): string {
    return this.selected()
      .map((candidate) => candidate.name)
      .join(', ');
  }

  /** Creates a profile you maintain, then lists and selects it (PRD 01). */
  protected async createPerson({ name, gender }: PersonDetails): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      const person = await this.people.create(name, gender);
      const candidate = this.remember({ id: person.id, ...name }, true);
      this.connections.update((list) => [...(list ?? []), candidate]);
      this.toggle(candidate, true);
      this.creatingPerson.set(false);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Links each selected person on their own: each is checked, consented to and audited apart, and
   * the people whose link failed stay selected to retry.
   */
  protected async add(): Promise<void> {
    const picked = this.selected();
    if (picked.length === 0) {
      this.selectionMissing.set(true);
      return;
    }
    const person = this.person();
    const relation = this.relation();
    const type = this.type();
    this.saving.set(true);
    this.error.set(null);
    this.outcomes.set([]);
    const outcomes: AddOutcome[] = [];
    const failed: Candidate[] = [];
    for (const other of picked) {
      try {
        const created =
          relation === 'spouse'
            ? await this.marriages.create({ spouseId: person.id, otherSpouseId: other.id })
            : await this.relationships.create(
                relation === 'parent'
                  ? { parentId: other.id, childId: person.id, type }
                  : { parentId: person.id, childId: other.id, type },
              );
        this.changed = true;
        outcomes.push({ name: other.name, state: created.isActive ? 'active' : 'pending' });
      } catch (error) {
        failed.push(other);
        outcomes.push({ name: other.name, state: 'failed', reason: problemMessage(error) });
      }
    }
    this.saving.set(false);
    if (failed.length === 0) {
      this.close(this.summary(outcomes));
      return;
    }
    this.selected.set(failed);
    if (outcomes.length === 1 && outcomes[0].state === 'failed') {
      this.error.set(outcomes[0].reason);
    } else {
      this.outcomes.set(outcomes);
    }
  }

  /** Closes the dialog. Escape closes it too, which ends here through its `close` event. */
  protected close(message: string | null = null): void {
    if (this.finished) {
      return;
    }
    this.finished = true;
    const dialog = this.dialog().nativeElement;
    if (dialog.open) {
      if (typeof dialog.close === 'function') {
        dialog.close();
      } else {
        dialog.removeAttribute('open');
      }
    }
    this.closed.emit({ changed: this.changed, message });
  }

  private summary(outcomes: AddOutcome[]): string {
    const pending = outcomes.filter((outcome) => outcome.state === 'pending').length;
    const active = outcomes.length - pending;
    const parts = [];
    if (active > 0) {
      parts.push(`Linked ${active} ${active === 1 ? 'person' : 'people'} to ${this.name()}.`);
    }
    if (pending > 0) {
      parts.push(
        pending === 1
          ? 'One link is saved but not active until the other maintainer approves it.'
          : `${pending} links are saved but not active until the other maintainers approve them.`,
      );
    }
    return parts.join(' ');
  }

  private async loadConnections(person: GraphPerson): Promise<void> {
    this.connections.set(null);
    this.error.set(null);
    try {
      const [maintained, overview] = await Promise.all([
        this.people.listMaintained(),
        person.maintainedByYou ? this.graph.overview() : Promise.resolve(null),
      ]);
      const own = maintained.people.map((profile) => this.remember(profile, true));
      const ownIds = new Set(own.map((candidate) => candidate.id));
      const linked = (overview?.people ?? [])
        .filter((other) => !other.placeholder && !ownIds.has(other.id))
        .map((other) =>
          this.remember(
            {
              id: other.id,
              firstName: other.firstName ?? '',
              middleName: other.middleName,
              lastName: other.lastName,
            },
            other.maintainedByYou,
          ),
        );
      if (this.person().id === person.id) {
        this.connections.set([...own, ...linked].filter((candidate) => candidate.id !== person.id));
        this.connectionsTruncated.set(maintained.isTruncated || Boolean(overview?.isTruncated));
      }
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }

  private async search(text: string): Promise<void> {
    try {
      const result = await this.people.search(text);
      if (this.query().trim() !== text) {
        return;
      }
      this.found.set(
        result.people
          .filter((other) => !other.placeholder && other.id !== this.person().id)
          .map((other) =>
            this.remember(
              {
                id: other.id,
                firstName: other.firstName ?? '',
                middleName: other.middleName,
                lastName: other.lastName,
              },
              other.maintainedByYou,
            ),
          ),
      );
      this.foundTruncated.set(result.isTruncated);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      if (this.query().trim() === text) {
        this.searching.set(false);
      }
    }
  }

  private remember(person: PersonName & { id: string }, maintainedByYou: boolean): Candidate {
    const name: PersonName = {
      firstName: person.firstName,
      middleName: person.middleName,
      lastName: person.lastName,
    };
    this.names.set(person.id, name);
    return { id: person.id, name: fullName(name), maintainedByYou };
  }

  private nameParts(candidate: Candidate): PersonName {
    return (
      this.names.get(candidate.id) ?? {
        firstName: candidate.name,
        middleName: null,
        lastName: null,
      }
    );
  }
}
