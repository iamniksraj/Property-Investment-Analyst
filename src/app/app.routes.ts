import { Routes } from '@angular/router';
import { DashboardComponent } from './dashboard/dashboard.component';
import { CompareComponent } from './compare/compare.component';
import { MarketComponent } from './market/market.component';
import { AnalystComponent } from './analyst/analyst.component';

export const routes: Routes = [
  { path: '', component: DashboardComponent },
  { path: 'compare', component: CompareComponent },
  { path: 'market', component: MarketComponent },
  { path: 'analyst', component: AnalystComponent },
  { path: '**', redirectTo: '' },
];
