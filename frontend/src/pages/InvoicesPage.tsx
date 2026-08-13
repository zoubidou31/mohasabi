import { useCallback, useEffect, useRef, useState } from 'react';
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
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { Eye, Filter, Plus, Search, X } from 'lucide-react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { Client, InvoiceStatus, PagedResult, InvoiceSummary } from '../api/types';
import { formatCurrency, formatDate } from '../utils/format';
import PageHeader from '../components/PageHeader';
import SearchSelect from '../components/SearchSelect';
import StatusBadge from '../components/StatusBadge';
import TablePaginationBar from '../components/TablePaginationBar';
import { COMMAND_IDS, useCommand } from '../utils/shortcuts';

export default function InvoicesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [data, setData] = useState<PagedResult<InvoiceSummary> | null>(null);
  const [search, setSearch] = useState(searchParams.get('search') ?? '');
  const [status, setStatus] = useState(searchParams.get('status') ?? '');
  const [client, setClient] = useState<Client | null>(null);
  const [clientId, setClientId] = useState(searchParams.get('clientId') ?? '');
  const [date, setDate] = useState(searchParams.get('from') ?? '');
  const [to, setTo] = useState(searchParams.get('to') ?? '');
  const [overdue, setOverdue] = useState(searchParams.get('overdue') === 'true');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(7);
  const [reload, setReload] = useState(0);
  const [loadError, setLoadError] = useState('');
  const searchRef = useRef<HTMLInputElement>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  useCommand(COMMAND_IDS.FOCUS_SEARCH, () => searchRef.current?.focus());
  // Entrée : ouvre la facture sélectionnée dans la liste.
  useCommand(COMMAND_IDS.OPEN_SELECTED, () => {
    const inv = data?.items.find((x) => x.id === selectedId);
    if (inv) navigate(`/invoices/${inv.id}`);
  });

  // Pré-remplit le filtre client depuis l'URL (navigation depuis les rapports).
  useEffect(() => {
    if (clientId && !client) {
      api
        .get<Client>(`/clients/${clientId}`)
        .then((r) => setClient(r.data))
        .catch(() => {});
    }
  }, [clientId, client]);

  const load = useCallback(async () => {
    const params = new URLSearchParams();
    if (search) params.set('search', search);
    if (status) params.set('status', status);
    const cid = client?.id ?? (clientId || undefined);
    if (cid) params.set('clientId', cid);
    if (date) params.set('from', date);
    if (to) params.set('to', to);
    if (overdue) params.set('overdue', 'true');
    params.set('page', String(page + 1));
    params.set('pageSize', String(pageSize));
    try {
      const { data: d } = await api.get<PagedResult<InvoiceSummary>>('/invoices', { params });
      setData(d);
      setLoadError('');
    } catch (err) {
      setLoadError(extractError(err));
    }
  }, [search, status, client, clientId, date, to, overdue, page, pageSize]);

  useEffect(() => {
    void load();
  }, [load, reload]);

  const hasFilters = Boolean(search || status || client || clientId || date || to || overdue);

  const resetFilters = () => {
    setSearch('');
    setStatus('');
    setClient(null);
    setClientId('');
    setDate('');
    setTo('');
    setOverdue(false);
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

      <Card sx={{ p: 2, mb: 3, backgroundColor: 'background.paper' }}>
        <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
          <TextField
            inputRef={searchRef}
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
          <Box sx={{ width: 220 }}>
            <SearchSelect<Client>
              endpoint="/clients"
              value={client}
              onChange={(val) => {
                setClient(val);
                setClientId('');
                setPage(0);
              }}
              getOptionLabel={(c) => c.displayName}
              label={t('invoice.client')}
              size="small"
              fullWidth
            />
          </Box>
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

      <TableContainer
        component={Card}
        sx={{ boxShadow: 'none' }}
        tabIndex={0}
        onKeyDown={(e) => {
          const items = data?.items ?? [];
          if (items.length === 0) return;
          if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault();
            const idx = items.findIndex((x) => x.id === selectedId);
            let next = e.key === 'ArrowDown' ? idx + 1 : idx - 1;
            if (idx === -1) next = 0;
            next = Math.max(0, Math.min(items.length - 1, next));
            setSelectedId(items[next].id);
          }
        }}
      >
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
              <TableRow key={inv.id} hover selected={selectedId === inv.id} sx={{ cursor: 'pointer' }} onClick={() => { setSelectedId(inv.id); navigate(`/invoices/${inv.id}`); }}>
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
        <TablePaginationBar
          count={data?.totalCount ?? 0}
          page={page}
          onPageChange={setPage}
          rowsPerPage={pageSize}
          onRowsPerPageChange={(size) => {
            setPageSize(size);
            setPage(0);
          }}
        />
      </TableContainer>
    </Box>
  );
}
