import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { PeopleService } from './people.service';
import { PersonName } from './person';
import { PersonNameForm } from './person-name-form';
import { problemMessage } from './problem';

/** Creates a profile maintained by the signed-in user. It is not linked to their account. */
@Component({
  selector: 'app-new-person-page',
  imports: [PersonNameForm],
  templateUrl: './new-person-page.html',
})
export class NewPersonPage {
  private readonly people = inject(PeopleService);
  private readonly router = inject(Router);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async create(name: PersonName): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      const person = await this.people.create(name);
      await this.router.navigate(['/people', person.id]);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): Promise<boolean> {
    return this.router.navigateByUrl('/people');
  }
}
