import { useCallback, useEffect, useState } from 'react';
import {
  Box,
  Card,
  CardContent,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Select,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { AlertTriangle, Banknote, FileText, Percent, Receipt, Wallet } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import type { InvoiceSummary, MonthlyReport, PagedResult, TVAReport } from '../api/types';
import { formatCurrency, formatDate } from '../utils/format';
import PageHeader from '../components/PageHeader';
import StatusBadge from '../components/StatusBadge';
import TablePaginationBar from '../components/TablePaginationBar';

const reportCards: { key: string; labelKey: string; icon: typeof FileText; tone: string }[] = [
  { key: 'invoiceCount', labelKey: 'reports.invoiceCount', icon: Receipt, tone: '#157347' },
  { key: 'totalHT', labelKey: 'invoice.totalHT', icon: Banknote, tone: '#1A56DB' },
  { key: 'totalTVA', labelKey: 'invoice.totalTVA', icon: Percent, tone: '#B45309' },
  { key: 'totalTTC', labelKey: 'invoice.totalTTC', icon: Wallet, tone: '#4F46E5' },
  { key: 'collected', labelKey: 'reports.collected', icon: Banknote, tone: '#0E7A5F' },
  { key: 'outstanding', labelKey: 'invoice.balance', icon: AlertTriangle, tone: '#C81E1E' },
];

export default function ReportsPage() {
  const { t } = useTranslation();
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [report, setReport] = useState<MonthlyReport | null>(null);
  const [monthlyInvoices, setMonthlyInvoices] = useState<PagedResult<InvoiceSummary> | null>(null);
  const [monthlyPage, setMonthlyPage] = useState(0);
  const [monthlyPageSize, setMonthlyPageSize] = useState(7);
  const [unpaid, setUnpaid] = useState<PagedResult<InvoiceSummary> | null>(null);
  const [unpaidPage, setUnpaidPage] = useState(0);
  const [unpaidPageSize, setUnpaidPageSize] = useState(7);

  const load = useCallback(async () => {
    try {
      const [{ data: monthly }, { data: monthlyList }, { data: unpaidList }] = await Promise.all([
        api.get<MonthlyReport>('/reports/monthly', { params: { year, month } }),
        api.get<PagedResult<InvoiceSummary>>('/reports/monthly/invoices', {
          params: { year, month, page: monthlyPage + 1, pageSize: monthlyPageSize },
        }),
        api.get<PagedResult<InvoiceSummary>>('/reports/unpaid/paged', {
          params: { page: unpaidPage + 1, pageSize: unpaidPageSize },
        }),
      ]);
      setReport(monthly);
      setMonthlyInvoices(monthlyList);
      setUnpaid(unpaidList);
    } catch {
      // handled silently
    }
  }, [year, month, monthlyPage, monthlyPageSize, unpaidPage, unpaidPageSize]);

  useEffect(() => {
    void load();
  }, [load]);

  const years = Array.from({ length: 5 }, (_, i) => year - 2 + i);

  return (
    <Box>
      <PageHeader title={t('reports.title')} description={t('reports.description')} />

      <Card sx={{ p: 2, mb: 3 }}>
        <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel>{t('reports.year')}</InputLabel>
            <Select label={t('reports.year')} value={year} onChange={(e) => { setYear(parseInt(String(e.target.value), 10)); setMonthlyPage(0); }}>
              {years.map((y) => (
                <MenuItem key={y} value={y}>
                  {y}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel>{t('reports.month')}</InputLabel>
            <Select label={t('reports.month')} value={month} onChange={(e) => { setMonth(parseInt(String(e.target.value), 10)); setMonthlyPage(0); }}>
              {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
                <MenuItem key={m} value={m}>
                  {m}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Box>
      </Card>

      {report && (
        <>
          <Grid container spacing={2} sx={{ mb: 3 }} className="stagger">
            {reportCards.map((card) => {
              const Icon = card.icon;
              const value =
                card.key === 'invoiceCount'
                  ? String(report.invoiceCount)
                  : formatCurrency(report[card.key as keyof MonthlyReport] as number);
              return (
                <Grid item xs={6} sm={4} md={2} key={card.key}>
                  <Card sx={{ p: 2, height: '100%', transition: 'box-shadow 0.2s ease, transform 0.2s ease', '&:hover': { boxShadow: 3, transform: 'translateY(-2px)' } }}>
                    <CardContent sx={{ p: '0 !important' }}>
                      <Box
                        sx={{
                          width: 38,
                          height: 38,
                          borderRadius: 2,
                          display: 'grid',
                          placeItems: 'center',
                          mb: 1.5,
                          color: '#fff',
                          backgroundColor: card.tone,
                          boxShadow: `0 6px 14px ${card.tone}33`,
                        }}
                      >
                        <Icon size={18} />
                      </Box>
                      <Typography variant="body2" color="text.secondary" sx={{ fontSize: 12.5 }}>
                        {t(card.labelKey)}
                      </Typography>
                      <Typography variant="h6" className="tnum" sx={{ fontWeight: 800, mt: 0.25 }}>
                        {value}
                      </Typography>
                    </CardContent>
                  </Card>
                </Grid>
              );
            })}
          </Grid>

          <Card sx={{ mb: 3 }}>
            <CardContent sx={{ p: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
                {t('reports.tvaByRate')}
              </Typography>
              <TableContainer component={Card} sx={{ boxShadow: 'none' }}>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>{t('product.tvaRate')}</TableCell>
                      <TableCell align="right">{t('invoice.totalHT')}</TableCell>
                      <TableCell align="right">{t('invoice.totalTVA')}</TableCell>
                      <TableCell align="right">{t('invoice.totalTTC')}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {report.tvaByRate.map((rate: TVAReport) => (
                      <TableRow key={rate.tvaRate}>
                        <TableCell>
                          <StatusBadge variant="neutral" label={t(`tvaLabels.${rate.tvaRate}` as never)} />
                        </TableCell>
                        <TableCell align="right" className="tnum">{formatCurrency(rate.totalHT)}</TableCell>
                        <TableCell align="right" className="tnum">{formatCurrency(rate.tvaAmount)}</TableCell>
                        <TableCell align="right" className="tnum" sx={{ fontWeight: 600 }}>{formatCurrency(rate.totalTTC)}</TableCell>
                      </TableRow>
                    ))}
                    {report.tvaByRate.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={4} align="center" sx={{ py: 3, color: 'text.secondary' }}>
                          {t('common.none')}
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </TableContainer>
            </CardContent>
          </Card>

          <Card sx={{ mb: 3 }}>
            <CardContent sx={{ p: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
                {t('reports.monthly')} — {month}/{year}
              </Typography>
              <TableContainer component={Card} sx={{ boxShadow: 'none' }}>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>{t('invoice.invoiceNumber')}</TableCell>
                      <TableCell>{t('invoice.client')}</TableCell>
                      <TableCell>{t('invoice.invoiceDate')}</TableCell>
                      <TableCell>{t('common.status')}</TableCell>
                      <TableCell align="right">{t('invoice.totalTTC')}</TableCell>
                      <TableCell align="right">{t('invoice.paid')}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {monthlyInvoices?.items.map((inv) => (
                      <TableRow key={inv.id} hover>
                        <TableCell sx={{ fontWeight: 600 }}>{inv.invoiceNumber}</TableCell>
                        <TableCell>{inv.clientName}</TableCell>
                        <TableCell>{formatDate(inv.invoiceDate)}</TableCell>
                        <TableCell>
                          <StatusBadge label={t(`statusLabels.${inv.status}`)} />
                        </TableCell>
                        <TableCell align="right" className="tnum" sx={{ fontWeight: 600 }}>{formatCurrency(inv.totalTTC)}</TableCell>
                        <TableCell align="right" className="tnum">{formatCurrency(inv.montantPaye)}</TableCell>
                      </TableRow>
                    ))}
                    {monthlyInvoices && monthlyInvoices.items.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={6} align="center" sx={{ py: 3, color: 'text.secondary' }}>
                          {t('common.none')}
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
                <TablePaginationBar
                  count={monthlyInvoices?.totalCount ?? 0}
                  page={monthlyPage}
                  onPageChange={setMonthlyPage}
                  rowsPerPage={monthlyPageSize}
                  onRowsPerPageChange={(size) => {
                    setMonthlyPageSize(size);
                    setMonthlyPage(0);
                  }}
                />
              </TableContainer>
            </CardContent>
          </Card>
        </>
      )}

      <Card>
        <CardContent sx={{ p: 3 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
            {t('reports.unpaid')}
          </Typography>
          <TableContainer component={Card} sx={{ boxShadow: 'none' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>{t('invoice.invoiceNumber')}</TableCell>
                  <TableCell>{t('invoice.client')}</TableCell>
                  <TableCell>{t('invoice.dueDate')}</TableCell>
                  <TableCell align="right">{t('invoice.totalTTC')}</TableCell>
                  <TableCell align="right">{t('invoice.balance')}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {unpaid?.items.map((inv) => (
                  <TableRow key={inv.id} hover>
                    <TableCell sx={{ fontWeight: 600 }}>{inv.invoiceNumber}</TableCell>
                    <TableCell>{inv.clientName}</TableCell>
                    <TableCell>{inv.dueDate ? formatDate(inv.dueDate) : '—'}</TableCell>
                    <TableCell align="right" className="tnum" sx={{ fontWeight: 600 }}>{formatCurrency(inv.totalTTC)}</TableCell>
                    <TableCell align="right" className="tnum" sx={{ color: 'error.main', fontWeight: 700 }}>{formatCurrency(inv.soldeRestant)}</TableCell>
                  </TableRow>
                ))}
                {(!unpaid || unpaid.items.length === 0) && (
                  <TableRow>
                    <TableCell colSpan={5} align="center" sx={{ py: 3, color: 'text.secondary' }}>
                      {t('common.none')}
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
            <TablePaginationBar
              count={unpaid?.totalCount ?? 0}
              page={unpaidPage}
              onPageChange={setUnpaidPage}
              rowsPerPage={unpaidPageSize}
              onRowsPerPageChange={(size) => {
                setUnpaidPageSize(size);
                setUnpaidPage(0);
              }}
            />
          </TableContainer>
        </CardContent>
      </Card>
    </Box>
  );
}
