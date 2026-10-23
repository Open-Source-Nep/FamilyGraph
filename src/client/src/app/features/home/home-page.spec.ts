import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { HomeGraph } from '../graph/home-graph';
import { PeopleService } from '../people/people.service';
import { PersonSummary } from '../people/person';
import { HomePage } from './home-page';

/** The graph itself is covered by the HomeGraph tests. */
@Component({ selector: 'app-home-graph', template: '' })
class StubHomeGraph {}

const profile = (id: string, firstName: string): PersonSummary => ({
  id,
  firstName,
  middleName: null,
  lastName: null,
  visibility: 'Public',
});

describe('HomePage', () => {
  let fixture: ComponentFixture<HomePage>;
  let profiles: PersonSummary[];

  beforeEach(() => {
    profiles = [profile('maya', 'Maya'), profile('gita', 'Gita')];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { account: signal({ email: 'a@example.org' }) } },
        {
          provide: PeopleService,
          useValue: {
            listMaintained: () => Promise.resolve({ people: profiles, isTruncated: false }),
          },
        },
      ],
    });
    TestBed.overrideComponent(HomePage, {
      remove: { imports: [HomeGraph] },
      add: { imports: [StubHomeGraph] },
    });
  });

  const show = async () => {
    fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();
  };
  const graph = () => fixture.debugElement.query((el) => el.name === 'app-home-graph');

  it('shows the graph of everyone connected to your profiles, with no centre picker', async () => {
    await show();

    expect(graph()).not.toBeNull();
    expect(fixture.nativeElement.querySelector('select')).toBeNull();
  });

  it('asks for a profile when you have none', async () => {
    profiles = [];
    await show();

    expect(graph()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('once you add a profile');
  });
});
