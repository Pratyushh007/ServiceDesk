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

export interface TicketComment {
  id: number;
  body: string;
  isStaff: boolean;
  createdAtUtc: string;
}

export interface TicketAttachment {
  id: number;
  fileName: string;
  sizeBytes: number;
  createdAtUtc: string;
}

export interface TicketActivity {
  comments: TicketComment[];
  attachments: TicketAttachment[];
  statusChanges: {
    id: number;
    previousStatus: Ticket['status'] | null;
    status: Ticket['status'];
    changedAtUtc: string;
  }[];
}

export interface DashboardSummary {
  total: number;
  open: number;
  inProgress: number;
  resolved: number;
  categories: { name: string; count: number }[];
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

  login(credentials: Credentials, admin: boolean): Promise<Session> {
    const endpoint = admin ? '/api/auth/admin/login' : '/api/auth/login';
    return firstValueFrom(this.http.post<Session>(endpoint, credentials));
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

  updateTicketStatus(session: Session, id: number, status: Ticket['status']): Promise<void> {
    return firstValueFrom(
      this.http.put<void>('/api/tickets/' + id, { status }, { headers: this.headers(session) }),
    );
  }

  updateTicketCategory(session: Session, id: number, categoryId: number | null): Promise<void> {
    return firstValueFrom(
      this.http.put<void>('/api/tickets/' + id + '/category', { categoryId }, { headers: this.headers(session) }),
    );
  }

  deleteTicket(session: Session, id: number): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>('/api/tickets/' + id, { headers: this.headers(session) }),
    );
  }

  activity(session: Session, id: number): Promise<TicketActivity> {
    return firstValueFrom(
      this.http.get<TicketActivity>('/api/tickets/' + id + '/activity', { headers: this.headers(session) }),
    );
  }

  addComment(session: Session, id: number, body: string): Promise<TicketComment> {
    return firstValueFrom(
      this.http.post<TicketComment>('/api/tickets/' + id + '/comments', { body }, { headers: this.headers(session) }),
    );
  }

  uploadAttachment(session: Session, id: number, file: File): Promise<TicketAttachment> {
    const form = new FormData();
    form.set('file', file);
    return firstValueFrom(
      this.http.post<TicketAttachment>('/api/tickets/' + id + '/attachments', form, {
        headers: this.headers(session),
      }),
    );
  }

  downloadAttachment(session: Session, id: number, attachmentId: number): Promise<Blob> {
    return firstValueFrom(
      this.http.get('/api/tickets/' + id + '/attachments/' + attachmentId, {
        headers: this.headers(session),
        responseType: 'blob',
      }),
    );
  }

  dashboard(session: Session): Promise<DashboardSummary> {
    return firstValueFrom(
      this.http.get<DashboardSummary>('/api/dashboard/summary', { headers: this.headers(session) }),
    );
  }

  ticketReport(session: Session, from: string, to: string): Promise<Blob> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return firstValueFrom(this.http.get('/api/reports/tickets.xlsx', {
      params,
      headers: this.headers(session),
      responseType: 'blob',
    }));
  }

  private headers(session: Session): Record<string, string> {
    return { Authorization: `Bearer ${session.accessToken}` };
  }
}
