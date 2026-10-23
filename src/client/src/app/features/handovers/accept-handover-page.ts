import { DatePipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { HandoverPreview } from './handover';
import { HandoversService } from './handovers.service';

/**
 * Opens a handover link (PRD 01): shows the profile it hands over and lets you accept it, which
 * makes you its maintainer. The secret is the URL fragment.
 */
@Component({
  selector: 'app-accept-handover-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './accept-handover-page.html',
})
export class AcceptHandoverPage {
  private readonly handovers = inject(HandoversService);
  private readonly router = inject(Router);
  private readonly fragment = toSignal(inject(ActivatedRoute).fragment);

  protected readonly preview = signal<HandoverPreview | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly fullName = fullName;

  constructor() {
    effect(() => {
      const token = this.fragment();
      this.preview.set(null);
      this.error.set(null);
      if (!token) {
        this.error.set('This handover link is incomplete. Ask for the whole link.');
        return;
      }
      this.handovers.preview(token).then(
        (preview) => this.preview.set(preview),
        (error: unknown) => this.error.set(problemMessage(error)),
      );
    });
  }

  protected async accept(): Promise<void> {
    const token = this.fragment();
    if (!token) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      const accepted = await this.handovers.accept(token);
      await this.router.navigate(['/people', accepted.personId]);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
