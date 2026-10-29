import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { DigitsPipe } from '../../core/i18n/local-format.pipes';
import { PersonLabel, PersonName, fullName } from '../people/person';
import { problemMessage } from '../people/problem';
import { Connections, GraphPerson, stepLabel } from './graph';
import { GraphService } from './graph.service';

/** Someone to find a connection to. */
export type ConnectionCandidate = (PersonName | PersonLabel) & { id: string };

/**
 * How a person is related to someone else, as text (PRD 08): the shortest path step by step, with
 * adoptive steps named (AC-08.7), and the further paths on request (AC-08.6). Until person search
 * (#22), the other person is chosen from the people shown and the profiles you maintain.
 */
@Component({
  selector: 'app-person-connections',
  imports: [DigitsPipe, NgTemplateOutlet, ReactiveFormsModule, RouterLink],
  templateUrl: './person-connections.html',
})
export class PersonConnections {
  private readonly graph = inject(GraphService);

  readonly from = input.required<GraphPerson>();
  readonly candidates = input.required<ConnectionCandidate[]>();

  protected readonly fullName = fullName;
  protected readonly stepLabel = stepLabel;
  protected readonly form = new FormGroup({
    otherId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  protected readonly others = computed(() =>
    this.candidates().filter((candidate) => candidate.id !== this.from().id),
  );
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
      this.form.reset();
    });
  }

  protected async find(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { otherId } = this.form.getRawValue();
    const from = this.fromId();
    const other = this.others().find((candidate) => candidate.id === otherId);
    this.searching.set(true);
    this.error.set(null);
    this.showMore.set(false);
    try {
      const result = await this.graph.connections(from, otherId);
      if (this.fromId() === from) {
        this.otherName.set(other ? fullName(other) : '');
        this.result.set(result);
      }
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.searching.set(false);
    }
  }
}
