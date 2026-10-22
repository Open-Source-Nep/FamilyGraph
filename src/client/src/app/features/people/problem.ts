import { HttpErrorResponse } from '@angular/common/http';

import { maxNamePartLength } from './person';

/** Wording for the server's error codes (ProblemDetails `code`, D-010). */
const messages: Record<string, string> = {
  'person.first_name_required': 'Enter a first name.',
  'person.not_maintainer': 'Only the maintainer of this profile can change it.',
  'person.not_found': 'This profile does not exist.',
};

export function problemMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const code: unknown = error.error?.code;
    if (typeof code === 'string' && code in messages) {
      return messages[code];
    }
    // Validation problems list the invalid fields; the only API-level rule is the length limit.
    if (error.status === 400 && error.error?.errors) {
      return `Each part of the name can be at most ${maxNamePartLength} characters.`;
    }
  }
  return 'Something went wrong. Please try again.';
}
