'use client';

import * as React from 'react';
import type { AssetCategory } from '@/lib/mock-catalog';
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
  category: AssetCategory;
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

  const managerLabel = category === 'BDR' ? 'Emissor / Gestora' : 'Gestora';
  const subCategoryLabel = category === 'FII' ? 'Segmento' : 'Categoria';

  return (
    <div className="flex flex-col gap-3 rounded-lg border bg-card p-3 sm:p-4 shadow-sm">
      {/* Primary Filter Bar */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-2.5 items-center">
        {/* Search input */}
        <div className="relative sm:col-span-2 lg:col-span-2">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
          <Input
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder={`Buscar por ticker, nome, ${category === 'ETF' ? 'benchmark ou ' : ''}gestora...`}
            className="pl-9 pr-8 text-xs h-9 bg-background"
            aria-label="Buscar ativos"
          />
          {search && (
            <button
              type="button"
              onClick={() => onSearchChange('')}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground p-0.5 rounded-sm"
              aria-label="Limpar busca"
            >
              <X className="size-3.5" />
            </button>
          )}
        </div>

        {/* Manager / Issuer Select */}
        <div className="w-full">
          <Select
            value={manager || 'all'}
            onValueChange={(val) => {
              if (typeof val === 'string') onManagerChange(val);
            }}
          >
            <SelectTrigger className="w-full h-9 text-xs justify-between bg-background">
              <span className="truncate">
                <span className="text-muted-foreground mr-1">{managerLabel}:</span>
                <SelectValue placeholder={`Todas (${managerOptions.length})`} />
              </span>
            </SelectTrigger>
            <SelectContent className="max-h-64">
              <SelectItem value="all">Todas as Gestoras ({managerOptions.length})</SelectItem>
              {managerOptions.map((opt) => (
                <SelectItem key={opt} value={opt}>
                  {opt}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* SubCategory / Segment Select */}
        <div className="w-full">
          <Select
            value={subCategory || 'all'}
            onValueChange={(val) => {
              if (typeof val === 'string') onSubCategoryChange(val);
            }}
          >
            <SelectTrigger className="w-full h-9 text-xs justify-between bg-background">
              <span className="truncate">
                <span className="text-muted-foreground mr-1">{subCategoryLabel}:</span>
                <SelectValue placeholder={`Todos (${subCategoryOptions.length})`} />
              </span>
            </SelectTrigger>
            <SelectContent className="max-h-64">
              <SelectItem value="all">Todos os Segmentos ({subCategoryOptions.length})</SelectItem>
              {subCategoryOptions.map((opt) => (
                <SelectItem key={opt} value={opt}>
                  {opt}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Active Filter Tags & Reset Bar */}
      <div className="flex flex-wrap items-center justify-between gap-2 pt-1 border-t text-xs">
        <div className="flex flex-wrap items-center gap-1.5 min-w-0">
          <span className="text-[11px] text-muted-foreground font-medium flex items-center gap-1">
            <SlidersHorizontal className="size-3" />
            Filtros ativos:
          </span>

          {!isFiltered && (
            <span className="text-[11px] text-muted-foreground italic">
              Nenhum (exibindo todos)
            </span>
          )}

          {search.trim() && (
            <Badge
              variant="secondary"
              className="gap-1 text-[11px] font-normal py-0 px-2 bg-muted hover:bg-muted"
            >
              Busca: &ldquo;{search}&rdquo;
              <button
                type="button"
                onClick={() => onSearchChange('')}
                className="hover:text-foreground"
                aria-label="Remover busca"
              >
                <X className="size-3" />
              </button>
            </Badge>
          )}

          {manager && manager !== 'all' && (
            <Badge
              variant="secondary"
              className="gap-1 text-[11px] font-normal py-0 px-2 bg-muted hover:bg-muted"
            >
              {managerLabel}: {manager}
              <button
                type="button"
                onClick={() => onManagerChange('all')}
                className="hover:text-foreground"
                aria-label={`Remover filtro de ${managerLabel}`}
              >
                <X className="size-3" />
              </button>
            </Badge>
          )}

          {subCategory && subCategory !== 'all' && (
            <Badge
              variant="secondary"
              className="gap-1 text-[11px] font-normal py-0 px-2 bg-muted hover:bg-muted"
            >
              {subCategoryLabel}: {subCategory}
              <button
                type="button"
                onClick={() => onSubCategoryChange('all')}
                className="hover:text-foreground"
                aria-label={`Remover filtro de ${subCategoryLabel}`}
              >
                <X className="size-3" />
              </button>
            </Badge>
          )}
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <span className="text-[11px] text-muted-foreground font-mono">
            {totalFiltered} de {totalAvailable} ativos
          </span>
          {isFiltered && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onResetFilters}
              className="h-7 px-2 text-xs text-muted-foreground hover:text-foreground gap-1"
            >
              <RotateCcw className="size-3" />
              Limpar filtros
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
