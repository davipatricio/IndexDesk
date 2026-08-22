'use client';

import * as React from 'react';
import type { CatalogCategory } from './catalog-table';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Search, X, SlidersHorizontal, RotateCcw } from 'lucide-react';

interface CatalogFiltersProps {
  category: CatalogCategory;
  search: string;
  onSearchChange: (value: string) => void;
  manager: string;
  onManagerChange: (value: string) => void;
  subCategory: string;
  onSubCategoryChange: (value: string) => void;
  managerOptions: string[];
  subCategoryOptions: string[];
  totalFiltered: number;
  totalAvailable: number;
  onResetFilters: () => void;
}

export function CatalogFilters({
  category,
  search,
  onSearchChange,
  manager,
  onManagerChange,
  subCategory,
  onSubCategoryChange,
  managerOptions,
  subCategoryOptions,
  totalFiltered,
  totalAvailable,
  onResetFilters,
}: CatalogFiltersProps) {
  const isFiltered =
    Boolean(search.trim()) ||
    (manager !== 'all' && Boolean(manager)) ||
    (subCategory !== 'all' && Boolean(subCategory));
  const managerLabel = category === 'BDR' ? 'Emissor / gestora' : 'Gestora';
  const subCategoryLabel = category === 'FII' ? 'Segmento' : 'Categoria';

  return (
    <div className="flex flex-col gap-3 rounded-lg border bg-card p-3 shadow-sm sm:p-4">
      <div className="grid grid-cols-1 items-center gap-2.5 sm:grid-cols-2 lg:grid-cols-4">
        <div className="relative sm:col-span-2 lg:col-span-2">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="Buscar por ticker, nome ou benchmark..."
            className="h-9 bg-background pl-9 pr-8 text-xs"
            aria-label="Buscar ativos"
          />
          {search ? (
            <button
              type="button"
              onClick={() => onSearchChange('')}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-sm p-0.5 text-muted-foreground hover:text-foreground"
              aria-label="Limpar busca"
            >
              <X className="size-3.5" />
            </button>
          ) : null}
        </div>
        <Select value={manager || 'all'} onValueChange={(value) => onManagerChange(value ?? 'all')}>
          <SelectTrigger className="h-9 w-full bg-background text-xs">
            <SelectValue placeholder={`${managerLabel} (${managerOptions.length})`} />
          </SelectTrigger>
          <SelectContent className="max-h-64">
            <SelectItem value="all">Todas as {managerLabel.toLowerCase()}s</SelectItem>
            {managerOptions.map((option) => (
              <SelectItem key={option} value={option}>
                {option}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={subCategory || 'all'}
          onValueChange={(value) => onSubCategoryChange(value ?? 'all')}
        >
          <SelectTrigger className="h-9 w-full bg-background text-xs">
            <SelectValue placeholder={`${subCategoryLabel} (${subCategoryOptions.length})`} />
          </SelectTrigger>
          <SelectContent className="max-h-64">
            <SelectItem value="all">Todas as opções</SelectItem>
            {subCategoryOptions.map((option) => (
              <SelectItem key={option} value={option}>
                {option}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-1 text-xs">
        <div className="flex min-w-0 flex-wrap items-center gap-1.5">
          <span className="flex items-center gap-1 text-[11px] font-medium text-muted-foreground">
            <SlidersHorizontal className="size-3" />
            Filtros ativos:
          </span>
          {!isFiltered ? (
            <span className="text-[11px] italic text-muted-foreground">Nenhum</span>
          ) : null}
          {search.trim() ? (
            <Badge variant="secondary" className="gap-1 px-2 py-0 text-[11px] font-normal">
              Busca: “{search}”
              <button type="button" onClick={() => onSearchChange('')} aria-label="Remover busca">
                <X className="size-3" />
              </button>
            </Badge>
          ) : null}
          {manager !== 'all' && manager ? (
            <Badge variant="secondary" className="gap-1 px-2 py-0 text-[11px] font-normal">
              {managerLabel}: {manager}
              <button
                type="button"
                onClick={() => onManagerChange('all')}
                aria-label={`Remover filtro de ${managerLabel}`}
              >
                <X className="size-3" />
              </button>
            </Badge>
          ) : null}
          {subCategory !== 'all' && subCategory ? (
            <Badge variant="secondary" className="gap-1 px-2 py-0 text-[11px] font-normal">
              {subCategoryLabel}: {subCategory}
              <button
                type="button"
                onClick={() => onSubCategoryChange('all')}
                aria-label={`Remover filtro de ${subCategoryLabel}`}
              >
                <X className="size-3" />
              </button>
            </Badge>
          ) : null}
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <span className="font-mono text-[11px] text-muted-foreground">
            {totalFiltered} de {totalAvailable} ativos
          </span>
          {isFiltered ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={onResetFilters}
              className="h-7 gap-1 px-2 text-xs text-muted-foreground hover:text-foreground"
            >
              <RotateCcw className="size-3" />
              Limpar filtros
            </Button>
          ) : null}
        </div>
      </div>
    </div>
  );
}
