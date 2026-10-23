import { DOCUMENT, DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';

import { problemMessage } from '../people/problem';
import { OpenHandover, handoverUrl } from './handover';
import { HandoversService } from './handovers.service';

/**
 * Hands a profile you maintain over to another account (PRD 01): you create a link and send it
 * yourself; whoever opens it signed in can accept and becomes the maintainer. The link is shown
 * once, because the server keeps only its hash; a new link replaces the open one.
 */
@Component({
  selector: 'app-person-handover',
  imports: [DatePipe],
  templateUrl: './person-handover.html',
})
export class PersonHandover {
  private readonly handovers = inject(HandoversService);
  private readonly document = inject(DOCUMENT);

  /** A profile the signed-in user maintains. */
  readonly personId = input.required<string>();

  protected readonly open = signal<OpenHandover | null>(null);
  /** The link just created, shown until the page changes. */
  protected readonly link = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const personId = this.personId();
      this.open.set(null);
      this.link.set(null);
      this.error.set(null);
      this.handovers.getOpen(personId).then(
        (open) => this.open.set(open),
        (error: unknown) => this.error.set(problemMessage(error)),
      );
    });
  }

  protected isExpired(open: OpenHandover): boolean {
    return new Date(open.expiresAt).getTime() <= Date.now();
  }

  protected async offer(): Promise<void> {
    await this.run(async () => {
      const created = await this.handovers.offer(this.personId());
      this.link.set(handoverUrl(this.document.location.origin, created.token));
      this.copied.set(false);
      this.open.set({ createdAt: new Date().toISOString(), expiresAt: created.expiresAt });
    });
  }

  protected async cancel(): Promise<void> {
    await this.run(async () => {
      await this.handovers.cancel(this.personId());
      this.open.set(null);
      this.link.set(null);
    });
  }

  protected async copy(link: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(link);
      this.copied.set(true);
    } catch {
      this.error.set('Copy the link from the box instead.');
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
