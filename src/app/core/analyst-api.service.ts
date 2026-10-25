import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiProblem } from './comparison-api.models';
import { AnalystAskRequest, AnalystAskResponse, AnalystStatus } from './analyst-api.models';

@Injectable({ providedIn: 'root' })
export class AnalystApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  status(): Observable<AnalystStatus> {
    return this.http
      .get<AnalystStatus>(`${this.baseUrl}/api/investment-analyst/status`)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  ask(body: AnalystAskRequest): Observable<AnalystAskResponse> {
    return this.http
      .post<AnalystAskResponse>(`${this.baseUrl}/api/investment-analyst/ask`, body)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  private toProblem(error: HttpErrorResponse): ApiProblem {
    const body = error.error as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
    } | null;
    const messages: string[] = [];
    if (body?.errors) {
      for (const value of Object.values(body.errors)) {
        if (Array.isArray(value)) {
          messages.push(...value);
        }
      }
    }
    if (messages.length === 0 && typeof body?.detail === 'string' && body.detail.trim()) {
      messages.push(body.detail);
    }
    if (messages.length === 0) {
      messages.push(
        error.status === 0
          ? 'The API is unreachable. Start the ASP.NET Core host and confirm the Angular apiBaseUrl.'
          : error.statusText || 'The request failed.',
      );
    }

    return {
      title: body?.title || 'Request failed',
      messages,
      status: error.status,
    };
  }
}
