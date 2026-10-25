import {
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  model,
  output,
  signal,
} from '@angular/core';

import { GraphService } from '../graph/graph.service';
import { PeopleService } from '../people/people.service';
import { PersonName, fullName } from '../people/person';
import { problemMessage } from '../people/problem';

/** Someone you can pick, by the name you see. */
export interface Candidate {
  id: string;
  name: string;
  maintainedByYou: boolean;
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
 * Picks people to link (D-032). The list starts with your connections: your own profiles and, when
 * `searchesEveryone`, the people you can see by name who are linked to them. Typing then also
 * searches every Public profile, so separate families can be joined. Otherwise only your own
 * profiles are listed, because you must maintain one of the two people (PRD 03).
 */
@Component({
  selector: 'app-person-picker',
  templateUrl: './person-picker.html',
  styleUrl: './person-picker.css',
})
export class PersonPicker {
  private readonly people = inject(PeopleService);
  private readonly graph = inject(GraphService);

  /** The person being linked, who is never offered. */
  readonly excludeId = input<string | null>(null);
  readonly searchesEveryone = input.required<boolean>();
  /** One person (radio buttons) instead of several (checkboxes). */
  readonly single = input(false);
  readonly legend = input('People');
  readonly hint = input('');
  /** Shows "Add a new person", which only asks the parent to create one; see `addCreated`. */
  readonly offersNewPerson = input(false);
  /** The people to add, in the order they were picked, kept while the list is filtered. */
  readonly selected = model<Candidate[]>([]);
  readonly newPersonRequested = output<void>();

  /** Your profiles, and the people linked to them when searching everyone; null while loading. */
  protected readonly connections = signal<Candidate[] | null>(null);
  protected readonly connectionsTruncated = signal(false);
  protected readonly query = signal('');
  /** Server matches for the current query, beyond your connections. */
  protected readonly found = signal<Candidate[]>([]);
  protected readonly foundTruncated = signal(false);
  protected readonly searching = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly name = 'person-picker-' + nextId++;

  protected readonly options = computed(() => {
    const text = this.query().trim();
    const local = (this.connections() ?? []).filter(
      (candidate) => !text || matchesName(this.nameParts(candidate), text),
    );
    const ids = new Set(local.map((candidate) => candidate.id));
    return [...local, ...this.found().filter((candidate) => !ids.has(candidate.id))];
  });

  /** Name parts by ID, for filtering your connections as you type. */
  private readonly names = new Map<string, PersonName>();
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.searchTimer));
    effect(() => void this.loadConnections(this.excludeId(), this.searchesEveryone()));
  }

  /** Lists a profile you just created and selects it (PRD 01). */
  addCreated(person: PersonName & { id: string }): void {
    const candidate = this.remember(person, true);
    this.connections.update((list) => [...(list ?? []), candidate]);
    this.toggle(candidate, true);
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
  }

  private async loadConnections(excludeId: string | null, everyone: boolean): Promise<void> {
    this.connections.set(null);
    this.error.set(null);
    try {
      const [maintained, overview] = await Promise.all([
        this.people.listMaintained(),
        everyone ? this.graph.overview() : Promise.resolve(null),
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
      if (this.excludeId() === excludeId) {
        this.connections.set([...own, ...linked].filter((candidate) => candidate.id !== excludeId));
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
          .filter((other) => !other.placeholder && other.id !== this.excludeId())
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

/** Keeps each picker's radio group apart when a page shows more than one. */
let nextId = 0;
