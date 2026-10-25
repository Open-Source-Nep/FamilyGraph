import { Component, computed, input } from '@angular/core';

import { PersonName, Placeholder, imageUrl, initials } from './person';

/** What an avatar needs: the person's ID, image and name, or the placeholder shown instead. */
export interface AvatarPerson extends PersonName {
  id: string;
  imageId: string | null;
  placeholder?: Placeholder | null;
}

/**
 * A small round photo of the person, or their initials when they have none (D-038). A placeholder
 * has neither, so it shows an empty circle. The name is next to it, so the avatar is decorative.
 */
@Component({
  selector: 'app-person-avatar',
  template: `
    @if (url(); as url) {
      <img [src]="url" alt="" loading="lazy" />
    } @else {
      {{ initials() }}
    }
  `,
  host: { class: 'avatar', 'aria-hidden': 'true' },
})
export class PersonAvatar {
  readonly person = input.required<AvatarPerson>();

  protected readonly url = computed(() =>
    this.person().placeholder ? null : imageUrl(this.person()),
  );
  protected readonly initials = computed(() =>
    this.person().placeholder ? '' : initials(this.person()),
  );
}
