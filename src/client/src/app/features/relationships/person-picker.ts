import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  model,
  output,
  signal,
  viewChild,
} from '@angular/core';

import { GraphService } from '../graph/graph.service';
import { PeopleService } from '../people/people.service';
import { Person, PersonDetails, PersonName, fullName } from '../people/person';
import { PersonAvatar } from '../people/person-avatar';
import { PersonNameForm } from '../people/person-name-form';
import { problemMessage } from '../people/problem';

/** Someone you can pick, by the name you see, with their photo and description to tell them apart (D-038). */
export interface Candidate {
  id: string;
  name: string;
  maintainedByYou: boolean;
  imageId: string | null;
  description: string | null;
}

/** What the picker lists a person by: their name parts, image and description. */
type Listed = PersonName & { id: string; imageId: string | null; description: string | null };

/** Searches start at this many characters, and wait this long after the last key. */
const minSearchLength = 2;
const searchDelayMs = 300;

/** The picker hint when everyone with a public profile can be found. */
export function pickerHintEveryone(): string {
  return $localize`:@@picker.hintEveryone:Your profiles and the people linked to them are listed first. Type a name to search everyone with a public profile.`;
}

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
 * Picks people to link (D-032), everywhere people are picked: parents, children and spouses, on
 * the person page and from the graph (D-038). The list starts with your connections: your own
 * profiles and, when `searchesEveryone`, the people you can see by name who are linked to them.
 * Typing then also searches every Public profile, so separate families can be joined. Otherwise
 * only your own profiles are listed, because you must maintain one of the two people (PRD 03).
 * Each row shows the photo or initials and the description. "Add a new person" opens a dialog that
 * creates a profile you maintain, then lists and selects it. The picker may sit inside another
 * form, as on the Add a person page, so the dialog keeps its own form's submit and reset events
 * from reaching that form.
 */
@Component({
  selector: 'app-person-picker',
  imports: [PersonAvatar, PersonNameForm],
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
  readonly legend = input($localize`:@@people.title:People`);
  readonly hint = input('');
  /** Shows "Add a new person". */
  readonly offersNewPerson = input(true);
  /** The people to add, in the order they were picked, kept while the list is filtered. */
  readonly selected = model<Candidate[]>([]);
  /** A profile created with "Add a new person", already listed and selected. */
  readonly created = output<Person>();

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
  protected readonly creatingPerson = signal(false);
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);
  private readonly newPersonDialog = viewChild<ElementRef<HTMLDialogElement>>('newPersonDialog');

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

  protected openNewPerson(): void {
    this.createError.set(null);
    this.creatingPerson.set(true);
    const dialog = this.newPersonDialog()?.nativeElement;
    // jsdom has no showModal, so fall back to a non-modal dialog there.
    if (dialog && !dialog.open) {
      if (typeof dialog.showModal === 'function') {
        dialog.showModal();
      } else {
        dialog.setAttribute('open', '');
      }
    }
  }

  /** Closes the dialog. Escape closes it too, which ends here through its `close` event. */
  protected closeNewPerson(): void {
    this.creatingPerson.set(false);
    const dialog = this.newPersonDialog()?.nativeElement;
    if (dialog?.open) {
      if (typeof dialog.close === 'function') {
        dialog.close();
      } else {
        dialog.removeAttribute('open');
      }
    }
  }

  /** Creates a profile you maintain, then lists and selects it (PRD 01). */
  protected async createPerson(details: PersonDetails): Promise<void> {
    this.creating.set(true);
    this.createError.set(null);
    try {
      const person = await this.people.create(details);
      // You maintain the new profile, so you see it by the name and description you gave it.
      const candidate = this.remember(
        { ...details.name, id: person.id, imageId: null, description: details.description },
        true,
      );
      this.connections.update((list) => [...(list ?? []), candidate]);
      this.toggle(candidate, true);
      this.closeNewPerson();
      this.created.emit(person);
    } catch (error) {
      this.createError.set(problemMessage(error));
    } finally {
      this.creating.set(false);
    }
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
      const own = maintained.people.map((profile) => this.remember(listed(profile), true));
      const ownIds = new Set(own.map((candidate) => candidate.id));
      const linked = (overview?.people ?? [])
        .filter((other) => !other.placeholder && !ownIds.has(other.id))
        .map((other) => this.remember(listed(other), other.maintainedByYou));
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
          .map((other) => this.remember(listed(other), other.maintainedByYou)),
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

  private remember(person: Listed, maintainedByYou: boolean): Candidate {
    const name: PersonName = {
      firstName: person.firstName,
      middleName: person.middleName,
      lastName: person.lastName,
    };
    this.names.set(person.id, name);
    return {
      id: person.id,
      name: fullName(name),
      maintainedByYou,
      imageId: person.imageId,
      description: person.description,
    };
  }

  /** The name parts of a candidate, for its avatar's initials. */
  protected avatar(candidate: Candidate): Listed {
    return { ...this.nameParts(candidate), ...candidate };
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

/** A person the server lets you see by name, as the picker lists them. */
function listed(person: {
  id: string;
  firstName: string | null;
  middleName: string | null;
  lastName: string | null;
  imageId: string | null;
  description: string | null;
}): Listed {
  return {
    id: person.id,
    firstName: person.firstName ?? '',
    middleName: person.middleName,
    lastName: person.lastName,
    imageId: person.imageId,
    description: person.description,
  };
}

/** Keeps each picker's radio group apart when a page shows more than one. */
let nextId = 0;
