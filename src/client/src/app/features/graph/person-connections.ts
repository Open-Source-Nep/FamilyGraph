import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { DigitsPipe } from '../../core/i18n/local-format.pipes';
import { PersonLabel, PersonName, fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { Candidate, PersonPicker, pickerHintEveryone } from '../relationships/person-picker';
import { Connections, GraphPerson, stepLabel } from './graph';
import { GraphService } from './graph.service';

/** Someone to find a connection to. */
export type ConnectionCandidate = (PersonName | PersonLabel) & { id: string };

/**
 * How a person is related to someone else, as text (PRD 08): the shortest path step by step, with
 * adoptive steps named (AC-08.7), and the further paths on request (AC-08.6). The other person
 * is chosen with the searchable people picker (D-050): the people shown in the graph, your
 * connections, and anyone with a public profile by name.
 */
@Component({
  selector: 'app-person-connections',
  imports: [DigitsPipe, NgTemplateOutlet, PersonPicker, RouterLink],
  templateUrl: './person-connections.html',
})
export class PersonConnections {
  private readonly graph = inject(GraphService);

  readonly from = input.required<GraphPerson>();
  readonly candidates = input.required<ConnectionCandidate[]>();

  protected readonly fullName = fullName;
  protected readonly stepLabel = stepLabel;
  protected readonly pickerHint = pickerHintEveryone();
  /** The other person, picked in the people picker. */
  protected readonly other = signal<Candidate[]>([]);
  /** Set when Find was pressed without a person. */
  protected readonly missing = signal(false);
  protected readonly result = signal<Connections | null>(null);
  protected readonly otherName = signal('');
  protected readonly showMore = signal(false);
  protected readonly searching = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly fromId = computed(() => this.from().id);

  constructor() {
    effect(() => {
      this.fromId();
      this.result.set(null);
      this.error.set(null);
      this.other.set([]);
      this.missing.set(false);
    });
  }

  protected async find(): Promise<void> {
    const other = this.other()[0];
    this.missing.set(!other);
    if (!other) {
      return;
    }
    const otherId = other.id;
    const from = this.fromId();
    this.searching.set(true);
    this.error.set(null);
    this.showMore.set(false);
    try {
      const result = await this.graph.connections(from, otherId);
      if (this.fromId() === from) {
        this.otherName.set(other.name);
        this.result.set(result);
      }
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.searching.set(false);
    }
  }
}
