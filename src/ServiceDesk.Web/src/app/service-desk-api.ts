import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface Credentials {
  email: string;
  password: string;
}

export interface Session {
  accessToken: string;
  expiresAtUtc: string;
  roles: string[];
}

export interface Category {
  id: number;
  name: string;
}

export interface TicketDraft {
  title: string;
  description: string;
  categoryId: number | null;
}

export interface Ticket extends TicketDraft {
  id: number;
  status: 'Open' | 'In Progress' | 'Resolved';
  createdAtUtc: string;
}

export interface TicketFilters {
  search: string;
  status: string;
  categoryId: string;
  sort: string;
}

@Injectable({ providedIn: 'root' })
export class ServiceDeskApi {
  private readonly http = inject(HttpClient);

  register(credentials: Credentials): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/auth/register', credentials));
  }

  login(credentials: Credentials): Promise<Session> {
    return firstValueFrom(this.http.post<Session>('/api/auth/login', credentials));
  }

  categories(session: Session): Promise<Category[]> {
    return firstValueFrom(
      this.http.get<Category[]>('/api/categories', { headers: this.headers(session) }),
    );
  }

  tickets(session: Session, filters: TicketFilters): Promise<Ticket[]> {
    let params = new HttpParams().set('sort', filters.sort);
    if (filters.search.trim()) {
      params = params.set('search', filters.search.trim());
    }
    if (filters.status) {
      params = params.set('status', filters.status);
    }
    if (filters.categoryId) {
      params = params.set('categoryId', filters.categoryId);
    }

    return firstValueFrom(
      this.http.get<Ticket[]>('/api/tickets', { params, headers: this.headers(session) }),
    );
  }

  createTicket(session: Session, draft: TicketDraft): Promise<Ticket> {
    return firstValueFrom(
      this.http.post<Ticket>('/api/tickets', draft, { headers: this.headers(session) }),
    );
  }

  private headers(session: Session): Record<string, string> {
    return { Authorization: `Bearer ${session.accessToken}` };
  }
}
