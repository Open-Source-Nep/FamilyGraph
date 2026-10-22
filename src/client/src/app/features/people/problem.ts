import { HttpErrorResponse } from '@angular/common/http';

import { maxNamePartLength } from './person';

/** Wording for the server's error codes (ProblemDetails `code`, D-010). */
const messages: Record<string, string> = {
  'person.first_name_required': 'Enter a first name.',
  'person.not_maintainer': 'Only the maintainer of this profile can change it.',
  'person.not_found': 'This profile does not exist.',
  'link.not_endpoint_maintainer':
    'You can only add a link when you maintain the parent or the child.',
  'link.duplicate': 'These two people are already linked as parent and child.',
  'link.self_link': 'A person cannot be their own parent.',
  'link.cycle_detected':
    'The child is already an ancestor of the parent, so this link would form a loop.',
  'link.cycle_unverifiable':
    'This link could not be checked for loops because the family tree above the parent is too large.',
  'link.not_found': 'This link does not exist.',
  'link.consent_not_maintainer': 'Only the maintainer of this profile can change its approval.',
  'link.consent_unchanged':
    'This link already has that approval. Reload the page to see its state.',
  'link.sole_maintainer_must_delete':
    'You maintain both people on this link, so delete it instead of declining or withdrawing it.',
  'link.not_sole_maintainer':
    'Only someone who maintains both people can delete this link. Withdraw your approval instead.',
  'link.deleted': 'This link has been deleted. Reload the page to see the current links.',
  'graph.timeout': 'The family graph took too long to load. Please try again.',
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
