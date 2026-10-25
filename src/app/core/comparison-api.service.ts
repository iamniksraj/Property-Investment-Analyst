import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ApiProblem,
  ComparisonDetailDto,
  ComparisonSummaryDto,
  SaveComparisonRequest,
} from './comparison-api.models';

@Injectable({ providedIn: 'root' })
export class ComparisonApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  list(): Observable<ComparisonSummaryDto[]> {
    return this.http
      .get<ComparisonSummaryDto[]>(`${this.baseUrl}/api/comparisons`)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  get(id: string): Observable<ComparisonDetailDto> {
    return this.http
      .get<ComparisonDetailDto>(`${this.baseUrl}/api/comparisons/${id}`)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  create(request: SaveComparisonRequest): Observable<ComparisonDetailDto> {
    return this.http
      .post<ComparisonDetailDto>(`${this.baseUrl}/api/comparisons`, request)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  update(id: string, request: SaveComparisonRequest): Observable<ComparisonDetailDto> {
    return this.http
      .put<ComparisonDetailDto>(`${this.baseUrl}/api/comparisons/${id}`, request)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  delete(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/api/comparisons/${id}`)
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
