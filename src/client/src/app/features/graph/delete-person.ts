import {
  Component,
  ElementRef,
  afterNextRender,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';

import { PeopleService } from '../people/people.service';
import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { GraphPerson } from './graph';

/**
 * Confirms and deletes a profile from the graph page's diagram (D-034), with the same rules as
 * the person page (PRD 05, D-026): the maintainer or an administrator deletes, the name, photo and
 * gender are erased for good, and the person stays as a `Deleted` placeholder with their links.
 * `closed` carries whether the profile was deleted.
 */
@Component({
  selector: 'app-delete-person',
  templateUrl: './delete-person.html',
})
export class DeletePerson {
  private readonly people = inject(PeopleService);

  readonly person = input.required<GraphPerson>();
  readonly closed = output<boolean>();

  protected readonly fullName = fullName;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private finished = false;

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
  }

  protected async confirm(): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.people.delete(this.person().id);
      this.close(true);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  /** Closes the dialog. Escape closes it too, which ends here through its `close` event. */
  protected close(deleted = false): void {
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
    this.closed.emit(deleted);
  }
}
