import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  Box,
  Button,
  FormControl,
  InputLabel,
  LinearProgress,
  MenuItem,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { AlertTriangle, ArrowRight, Download, Receipt, Users } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { InvoiceSummary, MonthlyReport, PagedResult, TopClient } from '../api/types';
import { formatCurrency, formatDate } from '../utils/format';
import StatusBadge from '../components/StatusBadge';
import { COMMAND_IDS, useCommand } from '../utils/shortcuts';

type Preset = 'thisMonth' | 'lastMonth' | 'thisQuarter' | 'thisYear' | 'custom';

function toDateParam(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function endOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth() + 1, 0);
}

function computePeriod(preset: Preset, year: number, month: number): { from: Date; to: Date } {
  const now = new Date();
  let from: Date;
  let to: Date;
  switch (preset) {
    case 'lastMonth': {
      const lm = new Date(now.getFullYear(), now.getMonth() - 1, 1);
      from = lm;
      to = endOfMonth(lm);
      break;
    }
    case 'thisQuarter': {
      const q = Math.floor(now.getMonth() / 3);
      from = new Date(now.getFullYear(), q * 3, 1);
      to = endOfMonth(new Date(now.getFullYear(), q * 3 + 2, 1));
      break;
    }
    case 'thisYear':
      from = new Date(now.getFullYear(), 0, 1);
      to = new Date(now.getFullYear(), 11, 31);
      break;
    case 'custom':
      from = new Date(year, month - 1, 1);
      to = endOfMonth(from);
      break;
    case 'thisMonth':
    default:
      from = new Date(now.getFullYear(), now.getMonth(), 1);
      to = endOfMonth(from);
      break;
  }
  return { from, to };
}

// Le backend renvoie le taux sous forme de libellé (« 19 % », « 9 % », « Exonéré »).
// On l'affiche en « TVA 19 % » sans jamais tomber sur une clé i18n brute.
function tvaRateDisplay(rate: string): string {
  if (rate === '19%') return 'TVA 19%';
  if (rate === '9%') return 'TVA 9%';
  return rate;
}

function MetricTile({
  label,
  value,
  accent,
  onClick,
}: {
  label: string;
  value: string;
  accent?: boolean;
  onClick: () => void;
}) {
  return (
    <Box
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onClick();
        }
      }}
      title={label}
      sx={{
        border: '1px solid',
        borderColor: 'divider',
        borderLeft: accent ? '3px solid' : '1px solid',
        borderLeftColor: accent ? 'primary.main' : 'divider',
        borderRadius: 2,
        p: 1.25,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        cursor: 'pointer',
        background: 'background.paper',
        transition: 'border-color 0.15s ease, background-color 0.15s ease',
        '&:hover': { borderColor: 'primary.main', background: 'action.hover' },
        '&:focus-visible': { outline: '2px solid', outlineColor: 'primary.main' },
      }}
    >
      <Typography
        variant="caption"
        color="text.secondary"
        sx={{ fontSize: 11, fontWeight: 600, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}
      >
        {label}
      </Typography>
      <Typography
        className="tnum"
        sx={{
          fontWeight: 800,
          fontSize: accent ? 22 : 18,
          mt: 0.25,
          lineHeight: 1.1,
          color: accent ? 'primary.main' : 'text.primary',
        }}
      >
        {value}
      </Typography>
    </Box>
  );
}

function Panel({
  title,
  icon,
  action,
  children,
  sx,
}: {
  title: string;
  icon?: ReactNode;
  action?: ReactNode;
  children: ReactNode;
  sx?: object;
}) {
  return (
    <Box
      sx={{
        border: '1px solid',
        borderColor: 'divider',
        borderRadius: 2,
        p: 1.5,
        display: 'flex',
        flexDirection: 'column',
        minHeight: 0,
        background: 'background.paper',
        ...sx,
      }}
    >
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 1,
          mb: 1,
          flexShrink: 0,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, minWidth: 0 }}>
          {icon}
          <Typography variant="subtitle2" sx={{ fontWeight: 700, fontSize: 13, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {title}
          </Typography>
        </Box>
        {action}
      </Box>
      <Box sx={{ flexGrow: 1, minHeight: 0, overflow: 'auto' }}>{children}</Box>
    </Box>
  );
}

function LinkButton({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <Button
      size="small"
      onClick={onClick}
      endIcon={<ArrowRight size={14} />}
      sx={{
        textTransform: 'none',
        fontSize: 12,
        fontWeight: 700,
        px: 0.5,
        color: 'primary.main',
        minWidth: 0,
        '&:hover': { background: 'transparent', textDecoration: 'underline' },
      }}
    >
      {label}
    </Button>
  );
}

function EmptyMini({ label }: { label: string }) {
  return (
    <Box sx={{ py: 1.5, textAlign: 'center', color: 'text.secondary', fontSize: 12.5 }}>{label}</Box>
  );
}

export default function ReportsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const now = new Date();
  const [preset, setPreset] = useState<Preset>('thisMonth');
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [report, setReport] = useState<MonthlyReport | null>(null);
  const [rangeInvoices, setRangeInvoices] = useState<PagedResult<InvoiceSummary> | null>(null);
  const [unpaid, setUnpaid] = useState<PagedResult<InvoiceSummary> | null>(null);
  const [topClients, setTopClients] = useState<TopClient[] | null>(null);

  const { from, to } = useMemo(() => computePeriod(preset, year, month), [preset, year, month]);
  const fromParam = toDateParam(from);
  const toParam = toDateParam(to);
  const periodQuery = `from=${fromParam}&to=${toParam}`;

  const periodRef = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    try {
      const [{ data: summary }, { data: rangeList }, { data: unpaidList }, { data: top }] = await Promise.all([
        api.get<MonthlyReport>('/reports/summary', { params: { from: fromParam, to: toParam } }),
        api.get<PagedResult<InvoiceSummary>>('/reports/range/invoices', {
          params: { from: fromParam, to: toParam, page: 1, pageSize: 5 },
        }),
        api.get<PagedResult<InvoiceSummary>>('/reports/unpaid/paged', {
          params: { page: 1, pageSize: 200 },
        }),
        api.get<TopClient[]>('/reports/top-clients', { params: { count: 3 } }),
      ]);
      setReport(summary);
      setRangeInvoices(rangeList);
      setUnpaid(unpaidList);
      setTopClients(top);
    } catch {
      // ignoré silencieusement
    }
  }, [fromParam, toParam]);

  useEffect(() => {
    void load();
  }, [load]);

  const years = Array.from({ length: 5 }, (_, i) => now.getFullYear() - 2 + i);

  const openPeriod = useCallback(() => navigate(`/invoices?${periodQuery}`), [navigate, periodQuery]);
  const openUnpaid = useCallback(() => navigate('/invoices?overdue=true'), [navigate]);

  const exportCsv = useCallback(() => {
    const unpaidItems = unpaid?.items ?? [];
    const unpaidTotal = unpaidItems.reduce((s, i) => s + (i.soldeRestant ?? 0), 0);
    const rows: string[][] = [];
    rows.push([t('reports.title')]);
    rows.push([t('reports.range'), `${fromParam} → ${toParam}`]);
    if (report) {
      rows.push([t('invoice.totalHT'), formatCurrency(report.totalHT)]);
      rows.push([t('invoice.totalTVA'), formatCurrency(report.totalTVA)]);
      rows.push([t('invoice.totalTTC'), formatCurrency(report.totalTTC)]);
      rows.push([t('reports.collected'), formatCurrency(report.totalCollected)]);
      rows.push([t('reports.toCollect'), formatCurrency(report.outstanding)]);
      rows.push([t('reports.impayed'), formatCurrency(unpaidTotal)]);
      rows.push([]);
      rows.push([t('reports.tvaByRate')]);
      rows.push([t('product.tvaRate'), t('invoice.totalHT'), t('invoice.totalTVA'), t('invoice.totalTTC')]);
      report.tvaByRate.forEach((r) =>
        rows.push([tvaRateDisplay(r.tvaRate), String(r.totalHT), String(r.tvaAmount), String(r.totalTTC)]),
      );
      rows.push([]);
      rows.push([t('reports.recentInvoices')]);
      rows.push([t('invoice.invoiceNumber'), t('invoice.client'), t('invoice.invoiceDate'), t('common.status'), t('invoice.totalTTC')]);
      (rangeInvoices?.items ?? []).forEach((inv) =>
        rows.push([inv.invoiceNumber, inv.clientName, inv.invoiceDate, t(`statusLabels.${inv.status}`), String(inv.totalTTC)]),
      );
      rows.push([]);
      rows.push([t('reports.unpaidWatch')]);
      rows.push([t('invoice.invoiceNumber'), t('invoice.client'), t('invoice.dueDate'), t('invoice.totalTTC'), t('invoice.balance')]);
      unpaidItems.forEach((inv) =>
        rows.push([inv.invoiceNumber, inv.clientName, inv.dueDate ?? '', String(inv.totalTTC), String(inv.soldeRestant)]),
      );
    }
    const csv = rows
      .map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(','))
      .join('\n');
    const blob = new Blob([`﻿${csv}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `rapport_${fromParam}_${toParam}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }, [t, fromParam, toParam, report, rangeInvoices, unpaid]);

  useCommand(COMMAND_IDS.EXPORT, exportCsv);
  useCommand(COMMAND_IDS.FOCUS_SEARCH, () => periodRef.current?.focus());

  const selectPreset = (value: Preset | null) => {
    if (!value) return;
    const p = computePeriod(value, year, month);
    setYear(p.from.getFullYear());
    setMonth(p.from.getMonth() + 1);
    setPreset(value);
  };

  const unpaidItems = unpaid?.items ?? [];
  const unpaidCount = unpaid?.totalCount ?? 0;
  const unpaidTotal = unpaidItems.reduce((s, i) => s + (i.soldeRestant ?? 0), 0);
  let nearestDue: string | null = null;
  for (const i of unpaidItems) {
    if (i.dueDate && (nearestDue === null || i.dueDate < nearestDue)) nearestDue = i.dueDate;
  }

  const collectionPct = report && report.totalTTC > 0
    ? Math.min(100, (report.totalCollected / report.totalTTC) * 100)
    : 0;

  return (
    <Box
      sx={{
        height: { xs: 'auto', md: '100%' },
        display: 'flex',
        flexDirection: 'column',
        gap: 1.5,
        minHeight: 0,
        overflow: { xs: 'visible', md: 'hidden' },
      }}
    >
      {/* HEADER */}
      <Box sx={{ flexShrink: 0 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 2, mb: 1 }}>
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: '-0.02em', fontSize: { xs: '1.4rem', md: '1.6rem' }, lineHeight: 1.2 }}>
              {t('reports.title')}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ fontSize: 12.5, mt: 0.25 }}>
              {t('reports.description')}
            </Typography>
          </Box>
          <Button
            variant="contained"
            size="small"
            disableElevation
            startIcon={<Download size={16} />}
            onClick={exportCsv}
            sx={{ flexShrink: 0 }}
          >
            {t('reports.export')}
          </Button>
        </Box>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
          <ToggleButtonGroup size="small" exclusive value={preset} onChange={(_, v) => selectPreset(v)}>
            <ToggleButton value="thisMonth">{t('reports.period.thisMonth')}</ToggleButton>
            <ToggleButton value="lastMonth">{t('reports.period.lastMonth')}</ToggleButton>
            <ToggleButton value="thisQuarter">{t('reports.period.thisQuarter')}</ToggleButton>
            <ToggleButton value="thisYear">{t('reports.period.thisYear')}</ToggleButton>
          </ToggleButtonGroup>
          <FormControl size="small" sx={{ minWidth: 110 }}>
            <InputLabel>{t('reports.month')}</InputLabel>
            <Select
              label={t('reports.month')}
              value={month}
              inputRef={periodRef}
              onChange={(e) => {
                setMonth(parseInt(String(e.target.value), 10));
                setPreset('custom');
              }}
            >
              {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
                <MenuItem key={m} value={m}>
                  {m}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 100 }}>
            <InputLabel>{t('reports.year')}</InputLabel>
            <Select
              label={t('reports.year')}
              value={year}
              onChange={(e) => {
                setYear(parseInt(String(e.target.value), 10));
                setPreset('custom');
              }}
            >
              {years.map((y) => (
                <MenuItem key={y} value={y}>
                  {y}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <Typography variant="body2" color="text.secondary" sx={{ ml: 'auto', fontSize: 12.5 }} className="tnum">
            {formatDate(fromParam)} → {formatDate(toParam)}
          </Typography>
        </Box>
      </Box>

      {report && (
        <>
          {/* FINANCIAL SUMMARY */}
          <Box
            sx={{
              flexShrink: 0,
              display: 'grid',
              gap: 1.25,
              gridTemplateColumns: { xs: 'repeat(2, 1fr)', sm: 'repeat(3, 1fr)', md: 'repeat(6, 1fr)' },
            }}
          >
            <MetricTile label={t('invoice.totalHT')} value={formatCurrency(report.totalHT)} onClick={openPeriod} />
            <MetricTile label={t('invoice.totalTVA')} value={formatCurrency(report.totalTVA)} onClick={openPeriod} />
            <MetricTile label={t('invoice.totalTTC')} value={formatCurrency(report.totalTTC)} accent onClick={openPeriod} />
            <MetricTile label={t('reports.collected')} value={formatCurrency(report.totalCollected)} onClick={openPeriod} />
            <MetricTile label={t('reports.toCollect')} value={formatCurrency(report.outstanding)} onClick={openPeriod} />
            <MetricTile label={t('reports.impayed')} value={formatCurrency(unpaidTotal)} onClick={openUnpaid} />
          </Box>

          {/* MAIN GRID */}
          <Box
            sx={{
              flexGrow: 1,
              minHeight: 0,
              display: 'grid',
              gap: 1.5,
              gridTemplateColumns: { xs: '1fr', md: '1.15fr 1fr 1fr' },
              gridTemplateRows: { md: '1fr 1fr' },
              overflow: { xs: 'visible', md: 'hidden' },
            }}
          >
            {/* ACTIVITÉ / PERFORMANCE */}
            <Panel title={t('reports.activity')} icon={<Receipt size={15} />} sx={{ gridColumn: { md: 1 }, gridRow: { md: 1 } }}>
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
                  <Typography variant="body2" color="text.secondary" sx={{ fontSize: 12.5 }}>
                    {t('reports.invoiceCount')}
                  </Typography>
                  <Typography variant="body1" className="tnum" sx={{ fontWeight: 800, fontSize: 18 }}>
                    {report.invoiceCount}
                  </Typography>
                </Box>
                <Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                    <Typography variant="caption" color="text.secondary">
                      {t('reports.collection')}
                    </Typography>
                    <Typography variant="caption" className="tnum" color="text.secondary">
                      {formatCurrency(report.totalCollected)} / {formatCurrency(report.totalTTC)}
                    </Typography>
                  </Box>
                  <LinearProgress variant="determinate" value={collectionPct} sx={{ height: 6, borderRadius: 3 }} />
                </Box>
              </Box>
            </Panel>

            {/* TVA */}
            <Panel title={t('reports.tvaByRate')} sx={{ gridColumn: { md: 1 }, gridRow: { md: 2 } }}>
              {report.tvaByRate.length === 0 ? (
                <EmptyMini label={t('common.none')} />
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ pl: 0, py: 0.5 }}>{t('product.tvaRate')}</TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}>{t('invoice.totalHT')}</TableCell>
                      <TableCell align="right" sx={{ py: 0.5 }}>{t('invoice.totalTVA')}</TableCell>
                      <TableCell align="right" sx={{ pr: 0, py: 0.5 }}>{t('invoice.totalTTC')}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {report.tvaByRate.map((rate) => (
                      <TableRow key={rate.tvaRate} sx={{ '&:last-child td': { border: 0 } }}>
                        <TableCell sx={{ pl: 0, py: 0.75 }}>
                          <StatusBadge variant="neutral" label={tvaRateDisplay(rate.tvaRate)} />
                        </TableCell>
                        <TableCell align="right" className="tnum" sx={{ py: 0.75 }}>{formatCurrency(rate.totalHT)}</TableCell>
                        <TableCell align="right" className="tnum" sx={{ py: 0.75 }}>{formatCurrency(rate.tvaAmount)}</TableCell>
                        <TableCell align="right" className="tnum" sx={{ pr: 0, py: 0.75, fontWeight: 600 }}>{formatCurrency(rate.totalTTC)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </Panel>

            {/* ACTIVITÉ RÉCENTE */}
            <Panel
              title={t('reports.recentInvoices')}
              action={<LinkButton label={t('reports.viewAll')} onClick={openPeriod} />}
              sx={{ gridColumn: { md: 2 }, gridRow: { md: '1 / span 2' } }}
            >
              {!rangeInvoices || rangeInvoices.items.length === 0 ? (
                <EmptyMini label={t('common.none')} />
              ) : (
                rangeInvoices.items.map((inv) => (
                  <Box
                    key={inv.id}
                    sx={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 1,
                      py: 0.75,
                      borderBottom: '1px solid',
                      borderColor: 'divider',
                      '&:last-child': { borderBottom: 0 },
                    }}
                  >
                    <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                      <Typography variant="body2" className="tnum" sx={{ fontWeight: 600, fontSize: 12.5, lineHeight: 1.2 }}>
                        {inv.invoiceNumber}
                      </Typography>
                      <Typography variant="caption" color="text.secondary" noWrap display="block" sx={{ fontSize: 11.5 }}>
                        {inv.clientName}
                      </Typography>
                    </Box>
                    <StatusBadge label={t(`statusLabels.${inv.status}`)} />
                    <Typography variant="body2" className="tnum" sx={{ fontWeight: 700, fontSize: 12.5, minWidth: 88, textAlign: 'right' }}>
                      {formatCurrency(inv.totalTTC)}
                    </Typography>
                  </Box>
                ))
              )}
            </Panel>

            {/* IMPAYÉS / À SURVEILLER */}
            <Panel
              title={t('reports.unpaidWatch')}
              icon={<AlertTriangle size={15} />}
              sx={{ gridColumn: { md: 3 }, gridRow: { md: 1 } }}
            >
              {unpaidCount === 0 ? (
                <EmptyMini label={t('reports.noUnpaid')} />
              ) : (
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, height: '100%' }}>
                  <Box sx={{ display: 'flex', gap: 2 }}>
                    <Box>
                      <Typography variant="caption" color="text.secondary" display="block">{t('reports.invoiceCount')}</Typography>
                      <Typography className="tnum" sx={{ fontWeight: 800, fontSize: 16 }}>{unpaidCount}</Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" color="text.secondary" display="block">{t('invoice.balance')}</Typography>
                      <Typography className="tnum" sx={{ fontWeight: 800, fontSize: 16, color: 'error.main' }}>{formatCurrency(unpaidTotal)}</Typography>
                    </Box>
                    {nearestDue && (
                      <Box>
                        <Typography variant="caption" color="text.secondary" display="block">{t('reports.nearestDue')}</Typography>
                        <Typography className="tnum" sx={{ fontWeight: 700, fontSize: 13 }}>{formatDate(nearestDue)}</Typography>
                      </Box>
                    )}
                  </Box>
                  <Box sx={{ flexGrow: 1, minHeight: 0, overflow: 'auto' }}>
                    {unpaidItems.slice(0, 3).map((inv) => (
                      <Box
                        key={inv.id}
                        sx={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: 1,
                          py: 0.5,
                          borderBottom: '1px solid',
                          borderColor: 'divider',
                          '&:last-child': { borderBottom: 0 },
                        }}
                      >
                        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                          <Typography variant="caption" className="tnum" sx={{ fontWeight: 600, fontSize: 11.5, lineHeight: 1.2 }} noWrap display="block">
                            {inv.invoiceNumber} · {inv.clientName}
                          </Typography>
                        </Box>
                        <Typography variant="caption" className="tnum" sx={{ fontWeight: 700, fontSize: 11.5, color: 'error.main' }}>
                          {formatCurrency(inv.soldeRestant)}
                        </Typography>
                      </Box>
                    ))}
                  </Box>
                  <Box sx={{ pt: 0.5 }}>
                    <LinkButton label={t('reports.viewUnpaid')} onClick={openUnpaid} />
                  </Box>
                </Box>
              )}
            </Panel>

            {/* TOP CLIENTS */}
            <Panel title={t('reports.topClients')} icon={<Users size={15} />} sx={{ gridColumn: { md: 3 }, gridRow: { md: 2 } }}>
              {!topClients || topClients.length === 0 ? (
                <EmptyMini label={t('common.none')} />
              ) : (
                topClients.map((c, i) => (
                  <Box
                    key={c.clientId}
                    onClick={() => navigate(`/invoices?clientId=${c.clientId}`)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        navigate(`/invoices?clientId=${c.clientId}`);
                      }
                    }}
                    sx={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 1,
                      py: 0.75,
                      borderBottom: '1px solid',
                      borderColor: 'divider',
                      cursor: 'pointer',
                      '&:last-child': { borderBottom: 0 },
                      '&:hover': { background: 'action.hover' },
                      '&:focus-visible': { outline: '2px solid', outlineColor: 'primary.main' },
                    }}
                  >
                    <Box
                      sx={{
                        width: 22,
                        height: 22,
                        borderRadius: '50%',
                        background: 'primary.light',
                        color: 'primary.main',
                        display: 'grid',
                        placeItems: 'center',
                        fontSize: 11,
                        fontWeight: 800,
                        flexShrink: 0,
                      }}
                    >
                      {i + 1}
                    </Box>
                    <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                      <Typography variant="body2" noWrap sx={{ fontWeight: 600, fontSize: 12.5, lineHeight: 1.2 }}>
                        {c.clientName}
                      </Typography>
                      <Typography variant="caption" color="text.secondary" className="tnum" display="block" sx={{ fontSize: 11.5 }}>
                        {c.invoiceCount}
                      </Typography>
                    </Box>
                    <Typography variant="body2" className="tnum" sx={{ fontWeight: 700, fontSize: 12.5 }}>
                      {formatCurrency(c.totalTTC)}
                    </Typography>
                  </Box>
                ))
              )}
            </Panel>
          </Box>
        </>
      )}
    </Box>
  );
}
