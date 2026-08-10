import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  FormControl,
  IconButton,
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
import { Building2, Pencil, Plus, Search, Trash2, UserRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { Client, ClientType, PaymentMethod, PagedResult } from '../api/types';
import { WILAYAS } from '../data/algerianData';
import { formatCurrency } from '../utils/format';
import PageHeader from '../components/PageHeader';
import StatusBadge from '../components/StatusBadge';

const emptyForm = {
  displayName: '',
  companyName: '',
  sector: '',
  nif: '',
  rc: '',
  art: '',
  address: '',
  postalCode: '',
  city: '',
  wilaya: '',
  phone: '',
  mobile: '',
  email: '',
  type: 'Entreprise' as ClientType,
  defaultPaymentMethod: 'Comptant' as PaymentMethod,
  notes: '',
};

export default function ClientsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [data, setData] = useState<PagedResult<Client> | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Client | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState('');
  const [alert, setAlert] = useState<{ severity: 'success' | 'error'; message: string } | null>(null);
  const [reload, setReload] = useState(0);

  // Delete-flow state (prevents double submission + shows a confirm step)
  const [confirmClient, setConfirmClient] = useState<Client | null>(null);
  const [deleting, setDeleting] = useState(false);

  const load = useCallback(async () => {
    try {
      const { data: d } = await api.get<PagedResult<Client>>('/clients', {
        params: { search: search || undefined, page: page + 1, pageSize },
      });
      setData(d);
    } catch {
      // handled silently
    }
  }, [search, page, pageSize]);

  useEffect(() => {
    void load();
  }, [load, reload]);

  const openNew = () => {
    setEditing(null);
    setForm(emptyForm);
    setError('');
    setDialogOpen(true);
  };

  const openEdit = (client: Client) => {
    setEditing(client);
    setForm({
      displayName: client.displayName,
      companyName: client.companyName ?? '',
      sector: client.sector ?? '',
      nif: client.nif ?? '',
      rc: client.rc ?? '',
      art: client.art ?? '',
      address: client.address,
      postalCode: client.postalCode ?? '',
      city: client.city ?? '',
      wilaya: client.wilaya ?? '',
      phone: client.phone,
      mobile: client.mobile ?? '',
      email: client.email ?? '',
      type: client.type,
      defaultPaymentMethod: client.defaultPaymentMethod ?? 'Comptant',
      notes: client.notes ?? '',
    });
    setError('');
    setDialogOpen(true);
  };

  const save = async () => {
    setError('');
    try {
      if (editing) {
        await api.put(`/clients/${editing.id}`, form);
      } else {
        await api.post('/clients', form);
      }
      setDialogOpen(false);
      setReload((x) => x + 1);
    } catch (err) {
      setError(extractError(err));
    }
  };

  const confirmDelete = (client: Client) => {
    setAlert(null);
    setConfirmClient(client);
  };

  const cancelDelete = () => {
    if (!deleting) setConfirmClient(null);
  };

  const executeDelete = async () => {
    if (deleting || !confirmClient) return;
    setDeleting(true);
    const { id, displayName } = confirmClient;
    try {
      await api.delete(`/clients/${id}`);
      // Only optimistically remove from the list on success; never cascade-delete documents.
      setData((prev) => prev ? { ...prev, items: prev.items.filter((c) => c.id !== id), totalCount: Math.max(0, prev.totalCount - 1) } : prev);
      setAlert({ severity: 'success', message: `${displayName} : ${t('common.deleted')}` });
    } catch (err) {
      // Surtout 409 : garder le client dans la liste et afficher le message réel du backend.
      setAlert({ severity: 'error', message: extractError(err) });
    } finally {
      setDeleting(false);
      setConfirmClient(null);
    }
  };

  const set = (field: keyof typeof emptyForm, value: string) => setForm((f) => ({ ...f, [field]: value }));

  return (
    <Box>
      <PageHeader
        title={t('client.title')}
        description={t('client.description')}
        action={
          <Button variant="contained" startIcon={<Plus size={18} />} onClick={openNew}>
            {t('client.newClient')}
          </Button>
        }
      />

      {alert && (
        <Alert severity={alert.severity} sx={{ mb: 2 }} onClose={() => setAlert(null)}>
          {alert.message}
        </Alert>
      )}

      <Card sx={{ p: 2, mb: 3 }}>
        <TextField
          label={t('common.search')}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(0);
          }}
          size="small"
          placeholder={t('client.displayName')}
          sx={{ minWidth: 300, maxWidth: 420 }}
        />
      </Card>

      <TableContainer component={Card} sx={{ boxShadow: 'none' }}>
        <Table size="medium">
          <TableHead>
            <TableRow>
              <TableCell>{t('client.displayName')}</TableCell>
              <TableCell>{t('client.type')}</TableCell>
              <TableCell>{t('client.nif')}</TableCell>
              <TableCell>{t('client.phone')}</TableCell>
              <TableCell>{t('client.city')}</TableCell>
              <TableCell align="right">{t('client.totalSpent')}</TableCell>
              <TableCell align="right">{t('client.outstanding')}</TableCell>
              <TableCell align="right">{t('common.actions')}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((c) => (
              <TableRow key={c.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/clients/${c.id}`)}>
                <TableCell>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
                    <Box
                      sx={{
                        width: 34,
                        height: 34,
                        borderRadius: 2,
                        display: 'grid',
                        placeItems: 'center',
                        backgroundColor: c.type === 'Entreprise' ? 'primary.light' : 'info.light',
                        color: c.type === 'Entreprise' ? 'primary.dark' : 'info.main',
                        flexShrink: 0,
                      }}
                    >
                      {c.type === 'Entreprise' ? <Building2 size={16} /> : <UserRound size={16} />}
                    </Box>
                    <Typography variant="body2" fontWeight={600}>
                      {c.displayName}
                    </Typography>
                  </Box>
                </TableCell>
                <TableCell>
                  <StatusBadge variant={c.type === 'Entreprise' ? 'success' : 'info'} label={t(`client.typeLabels.${c.type}`)} />
                </TableCell>
                <TableCell>{c.nif ?? '—'}</TableCell>
                <TableCell>{c.phone || '—'}</TableCell>
                <TableCell>{c.city ?? '—'}</TableCell>
                <TableCell align="right" className="tnum">{formatCurrency(c.totalSpent)}</TableCell>
                <TableCell align="right" className="tnum">{formatCurrency(c.totalSpent - c.totalSpent)}</TableCell>
                <TableCell align="right" onClick={(e) => e.stopPropagation()}>
                  <Box sx={{ display: 'inline-flex', gap: 0.5 }}>
                    <IconButton size="small" onClick={() => openEdit(c)}>
                      <Pencil size={16} />
                    </IconButton>
                    <IconButton size="small" sx={{ color: 'text.secondary', '&:hover': { color: 'error.main' } }} onClick={() => confirmDelete(c)}>
                      <Trash2 size={16} />
                    </IconButton>
                  </Box>
                </TableCell>
              </TableRow>
            ))}
            {data && data.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 6, color: 'text.secondary' }}>
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
          labelRowsPerPage={t('common.rowsPerPage')}
          rowsPerPageOptions={[10, 20, 50, 100]}
          sx={{ borderTop: '1px solid', borderColor: 'divider' }}
        />
      </TableContainer>

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} fullWidth maxWidth="md">
        <DialogTitle>{editing ? t('common.edit') : t('client.newClient')}</DialogTitle>
        <DialogContent>
          {error && (
            <Typography color="error" variant="body2" sx={{ mb: 1 }}>
              {error}
            </Typography>
          )}
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
            <TextField
              label={t('client.displayName')}
              fullWidth
              value={form.displayName}
              onChange={(e) => set('displayName', e.target.value)}
              required
            />
            <TextField select label={t('client.type')} fullWidth value={form.type} onChange={(e) => set('type', e.target.value)}>
              {(['Entreprise', 'Particulier', 'ProfessionnelLiberal'] as ClientType[]).map((ty) => (
                <MenuItem key={ty} value={ty}>
                  {t(`client.typeLabels.${ty}`)}
                </MenuItem>
              ))}
            </TextField>
            <TextField label={t('client.companyName')} fullWidth value={form.companyName} onChange={(e) => set('companyName', e.target.value)} />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label={t('client.nif')} fullWidth value={form.nif} onChange={(e) => set('nif', e.target.value)} />
              <TextField label={t('client.rc')} fullWidth value={form.rc} onChange={(e) => set('rc', e.target.value)} />
              <TextField label={t('client.art')} fullWidth value={form.art} onChange={(e) => set('art', e.target.value)} />
            </Box>
            <TextField label={t('client.address')} fullWidth value={form.address} onChange={(e) => set('address', e.target.value)} />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label={t('client.city')} fullWidth value={form.city} onChange={(e) => set('city', e.target.value)} />
              <FormControl fullWidth>
                <InputLabel>{t('client.wilaya')}</InputLabel>
                <Select
                  label={t('client.wilaya')}
                  value={form.wilaya}
                  onChange={(e) => {
                    const code = e.target.value;
                    set('wilaya', code ?? '');
                    // Lock the first two digits (wilaya code) as the postal code prefix.
                    set('postalCode', code ?? '');
                  }}
                >
                  <MenuItem value="">
                    <em>Sélectionner une wilaya</em>
                  </MenuItem>
                  {WILAYAS.map((w) => (
                    <MenuItem key={w.code} value={w.code}>
                      {w.code} — {w.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <TextField
                label={t('company.postalCode')}
                fullWidth
                value={
                  form.wilaya && (form.postalCode ?? '').startsWith(form.wilaya)
                    ? (form.postalCode ?? '').slice(form.wilaya.length)
                    : form.postalCode
                }
                placeholder={form.wilaya ? `${form.wilaya}___` : 'Code postal'}
                onChange={(e) => {
                  if (!form.wilaya) return;
                  // Only the 3-digit suffix is editable; the wilaya prefix is locked above.
                  const raw = e.target.value.replace(/[^\d]/g, '');
                  set('postalCode', form.wilaya + raw.slice(-3));
                }}
                inputProps={{
                  maxLength: form.wilaya ? 3 : 5,
                  inputMode: 'numeric',
                  pattern: '[0-9]*',
                }}
                disabled={!form.wilaya}
              />
            </Box>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label={t('client.phone')} fullWidth value={form.phone} onChange={(e) => set('phone', e.target.value)} />
              <TextField label={t('client.mobile')} fullWidth value={form.mobile} onChange={(e) => set('mobile', e.target.value)} />
              <TextField label={t('client.email')} fullWidth value={form.email} onChange={(e) => set('email', e.target.value)} />
            </Box>
            <TextField select label={t('invoice.paymentMethod')} fullWidth value={form.defaultPaymentMethod} onChange={(e) => set('defaultPaymentMethod', e.target.value)}>
              {(['Comptant', 'Cheque', 'VirementBancaire', 'CarteBancaire', 'Credit'] as PaymentMethod[]).map((pm) => (
                <MenuItem key={pm} value={pm}>
                  {t(`paymentLabels.${pm}`)}
                </MenuItem>
              ))}
            </TextField>
            <TextField label={t('invoice.notes')} fullWidth multiline rows={2} value={form.notes} onChange={(e) => set('notes', e.target.value)} />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>{t('common.cancel')}</Button>
          <Button variant="contained" onClick={() => void save()}>
            {t('common.save')}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={!!confirmClient} onClose={cancelDelete} maxWidth="xs" fullWidth>
        <DialogTitle>{t('common.delete')}</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {confirmClient
              ? `${t('common.deleteConfirm')} "${confirmClient.displayName}" ?`
              : ''}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={cancelDelete} disabled={deleting}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="contained"
            color="error"
            onClick={executeDelete}
            disabled={deleting}
            startIcon={deleting ? <CircularProgress size={16} /> : null}
          >
            {deleting ? t('common.loading') : t('common.delete')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
