import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiProblem } from './comparison-api.models';
import {
  AssumptionComparisonRequest,
  AssumptionComparisonResponse,
  MarketImportResult,
  MarketObservationDto,
  MarketProviderStatusResponse,
  MarketSearchResponse,
  MarketStatsResponse,
} from './market-api.models';

export interface MarketQuery {
  locality: string;
  propertyType?: string;
  configuration?: string;
  recordType?: string;
  areaType?: string;
  areaUnit?: string;
  verificationStatus?: string;
  includeSynthetic?: boolean;
  includeOutliers?: boolean;
  observedFrom?: string;
  observedTo?: string;
  skip?: number;
  take?: number;
}

@Injectable({ providedIn: 'root' })
export class MarketApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  search(filters: MarketQuery): Observable<MarketSearchResponse> {
    return this.http
      .get<MarketSearchResponse>(`${this.baseUrl}/api/market-observations`, { params: this.toParams(filters) })
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  stats(filters: MarketQuery): Observable<MarketStatsResponse> {
    return this.http
      .get<MarketStatsResponse>(`${this.baseUrl}/api/market-observations/stats`, { params: this.toParams(filters) })
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  compareAssumption(body: AssumptionComparisonRequest): Observable<AssumptionComparisonResponse> {
    return this.http
      .post<AssumptionComparisonResponse>(`${this.baseUrl}/api/market-observations/assumption-comparison`, body)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  providers(): Observable<MarketProviderStatusResponse> {
    return this.http
      .get<MarketProviderStatusResponse>(`${this.baseUrl}/api/market-providers`)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  get(id: string): Observable<MarketObservationDto> {
    return this.http
      .get<MarketObservationDto>(`${this.baseUrl}/api/market-observations/${id}`)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  preview(file: File): Observable<MarketImportResult> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http
      .post<MarketImportResult>(`${this.baseUrl}/api/market-observations/import/preview`, body)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  commit(file: File): Observable<MarketImportResult> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http
      .post<MarketImportResult>(`${this.baseUrl}/api/market-observations/import/commit`, body)
      .pipe(catchError((error) => throwError(() => this.toProblem(error))));
  }

  private toParams(filters: MarketQuery): HttpParams {
    let params = new HttpParams().set('locality', filters.locality);
    const optional: [string, string | number | boolean | undefined][] = [
      ['propertyType', filters.propertyType],
      ['configuration', filters.configuration],
      ['recordType', filters.recordType],
      ['areaType', filters.areaType],
      ['areaUnit', filters.areaUnit],
      ['verificationStatus', filters.verificationStatus],
      ['includeSynthetic', filters.includeSynthetic],
      ['includeOutliers', filters.includeOutliers],
      ['observedFrom', filters.observedFrom],
      ['observedTo', filters.observedTo],
      ['skip', filters.skip],
      ['take', filters.take],
    ];
    for (const [key, value] of optional) {
      if (value === undefined || value === '') {
        continue;
      }
      params = params.set(key, String(value));
    }
    return params;
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
