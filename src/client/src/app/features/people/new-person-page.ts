import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { Candidate, PersonPicker } from '../relationships/person-picker';
import { ParentChildType } from '../relationships/relationship';
import { RelationshipsService } from '../relationships/relationships.service';
import {
  LinkTarget,
  SpouseLinkChoices,
  SpouseLinkOutcome,
  createSpouseLinks,
} from '../relationships/spouse-link-choices';
import { PeopleService } from './people.service';
import { PersonDetails, fullName } from './person';
import { PersonNameForm } from './person-name-form';
import { problemMessage } from './problem';

/** What became of the new profile's links, shown when any is not active. */
interface Created {
  id: string;
  name: string;
  outcomes: SpouseLinkOutcome[];
}

/**
 * Creates a profile maintained by the signed-in user. It is not linked to their account. Parents
 * and children can be chosen on the way (#88, D-036): the new profile is yours, so anyone listed or
 * found can be picked, and each picked parent's spouses are offered as parents too. Each link is
 * created on its own after the profile, with the usual consent; a side maintained by someone else
 * waits for their approval.
 */
@Component({
  selector: 'app-new-person-page',
  imports: [PersonNameForm, PersonPicker, RouterLink, SpouseLinkChoices],
  templateUrl: './new-person-page.html',
})
export class NewPersonPage {
  private readonly people = inject(PeopleService);
  private readonly relationships = inject(RelationshipsService);
  private readonly router = inject(Router);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly choosingParents = signal(false);
  protected readonly choosingChildren = signal(false);
  protected readonly parents = signal<Candidate[]>([]);
  protected readonly children = signal<Candidate[]>([]);
  protected readonly parentType = signal<ParentChildType>('Biological');
  protected readonly childType = signal<ParentChildType>('Biological');
  protected readonly created = signal<Created | null>(null);
  /** The profile does not exist yet, so it has no spouses; you will maintain it. */
  protected readonly target = signal<LinkTarget>({
    id: null,
    name: 'the new person',
    maintainedByYou: true,
  });
  protected readonly parentNames = computed(() => names(this.parents()));
  protected readonly childNames = computed(() => names(this.children()));

  private readonly spouseChoices = viewChild(SpouseLinkChoices);

  protected async create({ name, gender }: PersonDetails): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    let person;
    try {
      person = await this.people.create(name, gender);
    } catch (error) {
      this.error.set(problemMessage(error));
      this.saving.set(false);
      return;
    }
    const outcomes = await this.link(person.id);
    this.saving.set(false);
    if (outcomes.every((outcome) => outcome.state === 'active' || outcome.state === 'existing')) {
      await this.router.navigate(['/people', person.id]);
    } else {
      this.created.set({ id: person.id, name: fullName(name), outcomes });
    }
  }

  protected cancel(): Promise<boolean> {
    return this.router.navigateByUrl('/people');
  }

  /** Links the picked parents, then the ticked spouses of those linked, then the picked children. */
  private async link(id: string): Promise<SpouseLinkOutcome[]> {
    const outcomes: SpouseLinkOutcome[] = [];
    const linkedParents: Candidate[] = [];
    const parentType = this.parentType();
    for (const parent of this.choosingParents() ? this.parents() : []) {
      const outcome = await this.createLink(
        parent.id,
        id,
        parentType,
        `${parent.name} as a parent`,
      );
      outcomes.push(outcome);
      if (outcome.state !== 'failed') {
        linkedParents.push(parent);
      }
    }
    const spouseLinks = this.spouseChoices()?.links(id, linkedParents) ?? [];
    outcomes.push(...(await createSpouseLinks(this.relationships, spouseLinks, parentType)));
    const childType = this.childType();
    for (const child of this.choosingChildren() ? this.children() : []) {
      outcomes.push(await this.createLink(id, child.id, childType, `${child.name} as a child`));
    }
    return outcomes;
  }

  private async createLink(
    parentId: string,
    childId: string,
    type: ParentChildType,
    name: string,
  ): Promise<SpouseLinkOutcome> {
    try {
      const link = await this.relationships.create({ parentId, childId, type });
      return { name, state: link.isActive ? 'active' : 'pending' };
    } catch (error) {
      return { name, state: 'failed', reason: problemMessage(error) };
    }
  }
}

function names(people: readonly Candidate[]): string {
  return people.map((person) => person.name).join(', ');
}
