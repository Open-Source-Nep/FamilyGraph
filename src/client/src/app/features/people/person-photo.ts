import { Component, computed, inject, input, output, signal } from '@angular/core';

import { PeopleService } from './people.service';
import { Person, fullName, imageUrl } from './person';
import { problemMessage } from './problem';
import { preparePhoto } from './profile-photo';

/**
 * The profile's photo, and for its maintainer "Add photo", "Change photo" and "Remove photo"
 * (PRD 01, D-028). The browser shrinks and re-encodes the photo before upload; the server
 * decides who may change it and never shows it for a placeholder.
 */
@Component({
  selector: 'app-person-photo',
  templateUrl: './person-photo.html',
  styleUrl: './person-photo.css',
})
export class PersonPhoto {
  private readonly people = inject(PeopleService);

  readonly person = input.required<Person>();
  /** The new image ID, or null once removed. */
  readonly changed = output<string | null>();

  protected readonly url = computed(() => imageUrl(this.person()));
  protected readonly alt = computed(() => `Photo of ${fullName(this.person())}`);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async choose(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      let photo: Blob;
      try {
        photo = await preparePhoto(file);
      } catch {
        this.error.set('This file could not be read as a photo. Choose a JPEG, PNG or WebP image.');
        return;
      }
      this.changed.emit(await this.people.setImage(this.person().id, photo));
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.people.removeImage(this.person().id);
      this.changed.emit(null);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
