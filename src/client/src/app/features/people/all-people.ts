import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { PeopleService } from './people.service';
import { Person, PersonName, fullName } from './person';
import { PersonAvatar } from './person-avatar';
import { problemMessage } from './problem';

/** A person the server lists by name; the list never holds placeholders. */
type NamedPerson = Person & PersonName;

/** How long typing pauses before the search asks the server. */
export const searchDelayMs = 300;

/**
 * The People page's All people tab (#22, D-042): everyone you may see by name, A to Z, a page at a
 * time with "Show more". The server decides who is listed: Public profiles, your own and, for
 * administrators, Private ones. The search box asks the server again once typing pauses; a reply to
 * an older search is dropped. A match is never the same person (PRD 06).
 */
@Component({
  selector: 'app-all-people',
  imports: [PersonAvatar, RouterLink],
  templateUrl: './all-people.html',
})
export class AllPeople {
  private readonly peopleService = inject(PeopleService);

  protected readonly fullName = fullName;
  protected readonly people = signal<NamedPerson[] | null>(null);
  protected readonly hasMore = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly query = signal('');

  private request = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
    void this.load(false);
  }

  protected search(text: string): void {
    this.query.set(text);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.load(false), searchDelayMs);
  }

  protected showMore(): void {
    void this.load(true);
  }

  /** "(yours)" and "(private)" after the name, or nothing. */
  protected marks(person: NamedPerson): string {
    return [
      person.maintainedByYou && $localize`:@@people.yoursMark:(yours)`,
      person.visibility === 'Private' && $localize`:@@people.privateMark:(private)`,
    ]
      .filter(Boolean)
      .join(' ');
  }

  /** Loads the first page, or the next one after those already shown. */
  private async load(more: boolean): Promise<void> {
    const request = ++this.request;
    const shown = more ? (this.people() ?? []) : [];
    this.loading.set(true);
    this.error.set(null);
    try {
      const page = await this.peopleService.browse(this.query(), shown.length);
      if (request !== this.request) {
        return;
      }
      // A profile added or deleted between pages can shift a row onto the next page twice.
      const known = new Set(shown.map((person) => person.id));
      const added = page.people.filter(
        (person): person is NamedPerson => person.firstName !== null && !known.has(person.id),
      );
      this.people.set([...shown, ...added]);
      this.hasMore.set(page.hasMore);
    } catch (error) {
      if (request === this.request) {
        this.error.set(problemMessage(error));
      }
    } finally {
      if (request === this.request) {
        this.loading.set(false);
      }
    }
  }
}
