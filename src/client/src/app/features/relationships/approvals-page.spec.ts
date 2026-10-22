import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ApprovalsPage } from './approvals-page';
import {
  EndpointApproval,
  LinkConsent,
  LinkConsentList,
  LinkEndpoint,
  LinkEndpointConsent,
} from './relationship';
import { RelationshipsService } from './relationships.service';

function endpoint(
  id: string,
  firstName: string,
  approval: EndpointApproval,
  maintainedByYou: boolean,
): LinkEndpointConsent {
  return { id, firstName, middleName: null, lastName: null, approval, maintainedByYou };
}

function link(id: string, parent: LinkEndpointConsent, child: LinkEndpointConsent): LinkConsent {
  return { id, type: 'Biological', parent, child, isActive: false };
}

describe('ApprovalsPage', () => {
  let fixture: ComponentFixture<ApprovalsPage>;
  let list: LinkConsentList;
  let calls: [string, string, LinkEndpoint][];

  const consent = (action: string) => (linkId: string, side: LinkEndpoint) => {
    calls.push([action, linkId, side]);
    const found = list.links.find((item) => item.id === linkId)!;
    return Promise.resolve({ ...found, isActive: action === 'approve' });
  };

  const render = async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: RelationshipsService,
          useValue: {
            listInactive: () => Promise.resolve(list),
            approve: consent('approve'),
            disagree: consent('disagree'),
            delete: (linkId: string) => {
              calls.push(['delete', linkId, 'Parent']);
              return Promise.resolve();
            },
          },
        },
      ],
    });
    fixture = TestBed.createComponent(ApprovalsPage);
    await fixture.whenStable();
  };

  const text = () => (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ') ?? '';

  const buttons = () =>
    [...fixture.nativeElement.querySelectorAll('button')].map((element: HTMLButtonElement) =>
      element.textContent?.trim(),
    );

  const click = async (label: string) => {
    (
      [...fixture.nativeElement.querySelectorAll('button')].find(
        (element: HTMLButtonElement) => element.textContent?.trim() === label,
      ) as HTMLButtonElement
    ).click();
    await fixture.whenStable();
  };

  beforeEach(() => {
    calls = [];
    list = {
      links: [
        // Bob linked his Ram as a parent of your Hari.
        link(
          'l1',
          endpoint('p1', 'Ram', 'Approved', false),
          endpoint('p2', 'Hari', 'Pending', true),
        ),
        // You withdrew your approval of Bina as a parent of your Maya.
        link(
          'l2',
          endpoint('p3', 'Bina', 'Approved', false),
          endpoint('p4', 'Maya', 'Disagreed', true),
        ),
        // You linked Gita, whom Bob maintains, as a parent of your Maya.
        link(
          'l3',
          endpoint('p5', 'Gita', 'Pending', false),
          endpoint('p4', 'Maya', 'Approved', true),
        ),
      ],
      isTruncated: false,
    };
  });

  it('groups the links by what they wait for', async () => {
    await render();

    const headings = [...fixture.nativeElement.querySelectorAll('h2')].map((h: HTMLElement) =>
      h.textContent?.trim(),
    );
    expect(headings).toEqual([
      'Waiting for your approval',
      'Declined or withdrawn by you',
      'Waiting for others',
    ]);
    expect(buttons()).toEqual(['Approve', 'Decline', 'Restore approval']);
    expect(text()).toContain('Waiting for the maintainer of Gita to approve.');
  });

  it('approves your pending side', async () => {
    await render();

    await click('Approve');

    expect(calls).toEqual([['approve', 'l1', 'Child']]);
    expect(text()).toContain('Ram as parent of Hari is now active.');
  });

  it('declines your pending side', async () => {
    await render();

    await click('Decline');

    expect(calls).toEqual([['disagree', 'l1', 'Child']]);
    expect(text()).toContain('You declined Ram as parent of Hari.');
  });

  it('restores an approval you withdrew', async () => {
    await render();

    await click('Restore approval');

    expect(calls).toEqual([['approve', 'l2', 'Child']]);
  });

  it('offers approval and deletion, not declining, when you maintain both people', async () => {
    list = {
      links: [
        link(
          'l4',
          endpoint('p6', 'Sita', 'Pending', true),
          endpoint('p4', 'Maya', 'Approved', true),
        ),
      ],
      isTruncated: false,
    };
    await render();

    expect(buttons()).toEqual(['Approve for Sita', 'Delete link']);
  });

  it('deletes a link whose two people you maintain after confirming', async () => {
    list = {
      links: [
        link(
          'l4',
          endpoint('p6', 'Sita', 'Disagreed', true),
          endpoint('p4', 'Maya', 'Approved', true),
        ),
      ],
      isTruncated: false,
    };
    await render();

    await click('Delete link');
    expect(text()).toContain('Delete this link? This cannot be undone.');
    expect(calls).toEqual([]);

    await click('Delete');

    expect(calls).toEqual([['delete', 'l4', 'Parent']]);
    expect(text()).toContain('You deleted Sita as parent of Maya.');
  });

  it('offers no deletion when someone else maintains one of the people', async () => {
    await render();

    expect(buttons()).not.toContain('Delete link');
  });

  it('says when nothing is waiting', async () => {
    list = { links: [], isTruncated: false };
    await render();

    expect(text()).toContain('Nothing is waiting.');
  });
});
