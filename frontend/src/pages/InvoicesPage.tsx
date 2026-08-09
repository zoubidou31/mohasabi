import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { Eye, Filter, Plus, Search, X } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { Client, InvoiceStatus, PagedResult, InvoiceSummary } from '../api/types';
import { formatCurrency, formatDate } from '../utils/format';
import PageHeader from '../components/PageHeader';
import StatusBadge from '../components/StatusBadge';

export default function InvoicesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [data, setData] = useState<PagedResult<InvoiceSummary> | null>(null);
  const [clients, setClients] = useState<Client[]>([]);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [clientId, setClientId] = useState('');
  const [date, setDate] = useState('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);
  const [reload, setReload] = useState(0);
  const [loadError, setLoadError] = useState('');

  const load = useCallback(async () => {
    const params = new URLSearchParams();
    if (search) params.set('search', search);
    if (status) params.set('status', status);
    if (clientId) params.set('clientId', clientId);
    if (date) params.set('from', date);
    params.set('page', String(page + 1));
    params.set('pageSize', String(pageSize));
    try {
      const { data: d } = await api.get<PagedResult<InvoiceSummary>>('/invoices', { params });
      setData(d);
      setLoadError('');
    } catch (err) {
      setLoadError(extractError(err));
    }
  }, [search, status, clientId, date, page, pageSize]);

  useEffect(() => {
    void load();
  }, [load, reload]);

  useEffect(() => {
    api.get<Client[]>('/clients').then(({ data }) => setClients(data)).catch(() => {});
  }, []);

  const hasFilters = Boolean(search || status || clientId || date);

  const resetFilters = () => {
    setSearch('');
    setStatus('');
    setClientId('');
    setDate('');
    setPage(0);
  };

  return (
    <Box>
      <PageHeader
        title={t('invoice.title')}
        description={t('invoice.description')}
        action={
          <Button variant="contained" startIcon={<Plus size={18} />} onClick={() => navigate('/invoices/new')}>
            {t('invoice.newInvoice')}
          </Button>
        }
      />

      <Card sx={{ p: 2, mb: 3, backgroundColor: '#FFFFFF' }}>
        <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
          <TextField
            label={t('common.search')}
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(0);
            }}
            size="small"
            placeholder={t('invoice.invoiceNumber')}
            sx={{ minWidth: 220, flexGrow: 1, maxWidth: 340 }}
          />
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel>{t('common.status')}</InputLabel>
            <Select label={t('common.status')} value={status} onChange={(e) => { setStatus(e.target.value); setPage(0); }}>
              <MenuItem value="">{t('common.all')}</MenuItem>
              {(['Brouillon', 'Finalisee', 'Payee', 'Annulee'] as InvoiceStatus[]).map((s) => (
                <MenuItem key={s} value={s}>
                  {t(`statusLabels.${s}`)}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 200 }}>
            <InputLabel>{t('invoice.client')}</InputLabel>
            <Select label={t('invoice.client')} value={clientId} onChange={(e) => { setClientId(e.target.value); setPage(0); }}>
              <MenuItem value="">{t('common.all')}</MenuItem>
              {clients.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.displayName}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <TextField
            type="date"
            size="small"
            label={t('common.date')}
            value={date}
            onChange={(e) => {
              setDate(e.target.value);
              setPage(0);
            }}
            InputLabelProps={{ shrink: true }}
            sx={{ width: 180 }}
          />
          <Button variant="contained" color="primary" startIcon={<Filter size={16} />} onClick={() => setReload((x) => x + 1)}>
            {t('common.filter')}
          </Button>
          {hasFilters && (
            <Button variant="text" startIcon={<X size={16} />} onClick={resetFilters} sx={{ color: 'text.secondary' }}>
              {t('common.reset')}
            </Button>
          )}
        </Box>
      </Card>

      {loadError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {loadError}
        </Alert>
      )}

      <TableContainer component={Card} sx={{ boxShadow: 'none' }}>
        <Table size="medium">
          <TableHead>
            <TableRow>
              <TableCell>{t('invoice.invoiceNumber')}</TableCell>
              <TableCell>{t('invoice.client')}</TableCell>
              <TableCell>{t('invoice.invoiceDate')}</TableCell>
              <TableCell>{t('invoice.dueDate')}</TableCell>
              <TableCell>{t('invoice.type')}</TableCell>
              <TableCell>{t('common.status')}</TableCell>
              <TableCell align="right">{t('invoice.totalTTC')}</TableCell>
              <TableCell align="right">{t('invoice.balance')}</TableCell>
              <TableCell align="center" width={90}>
                {t('common.actions')}
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((inv) => (
              <TableRow key={inv.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/invoices/${inv.id}`)}>
                <TableCell sx={{ fontWeight: 600 }}>{inv.invoiceNumber}</TableCell>
                <TableCell>{inv.clientName}</TableCell>
                <TableCell>{formatDate(inv.invoiceDate)}</TableCell>
                <TableCell>{inv.dueDate ? formatDate(inv.dueDate) : '—'}</TableCell>
                <TableCell>
                  <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, color: 'text.secondary', fontSize: 13 }}>
                    {t(`typeLabels.${inv.invoiceType}`)}
                  </Box>
                </TableCell>
                <TableCell>
                  <StatusBadge label={t(`statusLabels.${inv.status}`)} />
                </TableCell>
                <TableCell align="right" className="tnum" sx={{ fontWeight: 700 }}>
                  {formatCurrency(inv.totalTTC)}
                </TableCell>
                <TableCell align="right" className="tnum">
                  {formatCurrency(inv.soldeRestant)}
                </TableCell>
                <TableCell align="center">
                  <Box
                    onClick={(e) => e.stopPropagation()}
                    sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}
                  >
                    <Box
                      onClick={() => navigate(`/invoices/${inv.id}`)}
                      title={t('invoice.title')}
                      sx={{
                        display: 'grid',
                        placeItems: 'center',
                        width: 32,
                        height: 32,
                        borderRadius: 2,
                        color: 'text.secondary',
                        cursor: 'pointer',
                        transition: 'all 0.2s ease',
                        '&:hover': { backgroundColor: 'primary.light', color: 'primary.main' },
                      }}
                    >
                      <Eye size={16} />
                    </Box>
                  </Box>
                </TableCell>
              </TableRow>
            ))}
            {data && data.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={9} align="center" sx={{ py: 6, color: 'text.secondary' }}>
                  <Search size={28} style={{ opacity: 0.4, marginBottom: 8 }} />
                  <Typography variant="body2">{t('common.none')}</Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
        <TablePagination
          component="div"
          count={data?.totalCount ?? 0}
          page={page}
          onPageChange={(_, p) => setPage(p)}
          rowsPerPage={pageSize}
          onRowsPerPageChange={(e) => {
            setPageSize(parseInt(e.target.value, 10));
            setPage(0);
          }}
          labelRowsPerPage={t('common.filter')}
          rowsPerPageOptions={[10, 20, 50, 100]}
          sx={{ borderTop: '1px solid', borderColor: 'divider' }}
        />
      </TableContainer>
    </Box>
  );
}
