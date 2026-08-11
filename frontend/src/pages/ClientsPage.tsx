import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  Chip,
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
import {
  AlertTriangle,
  Building2,
  CheckCircle2,
  Pencil,
  Plus,
  Search,
  Trash2,
  UserRound,
  XCircle,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { Client, ClientType, PaymentMethod, PagedResult } from '../api/types';
import { formatCurrency } from '../utils/format';
import PageHeader from '../components/PageHeader';
import StatusBadge from '../components/StatusBadge';
import { SHORTCUT_EVENTS, useShortcutEvent } from '../utils/shortcuts';
import { getCommunes, getPostalCodes, getWilayas } from '../data/algeriaLocations';
import type { ClientForm, FieldMark } from '../utils/clientValidation';
import { markFor, operatorOf, validateClientForm } from '../utils/clientValidation';

const emptyForm: ClientForm = {
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
  type: 'Entreprise',
  defaultPaymentMethod: 'Comptant',
  notes: '',
};

function MarkIcon({ mark }: { mark: FieldMark }) {
  if (mark === 'ok') return <CheckCircle2 size={18} color="#2e7d32" />;
  if (mark === 'warn') return <AlertTriangle size={18} color="#ed6c02" />;
  if (mark === 'error') return <XCircle size={18} color="#d32f2f" />;
  return null;
}

export default function ClientsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [data, setData] = useState<PagedResult<Client> | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Client | null>(null);
  const [form, setForm] = useState<ClientForm>(emptyForm);
  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const [error, setError] = useState('');
  const [alert, setAlert] = useState<{ severity: 'success' | 'error'; message: string } | null>(null);
  const [reload, setReload] = useState(0);
  const searchRef = useRef<HTMLInputElement>(null);

  useShortcutEvent(SHORTCUT_EVENTS.FOCUS_SEARCH, () => searchRef.current?.focus());

  // Delete-flow state (prevents double submission + shows a confirm step)
  const [confirmClient, setConfirmClient] = useState<Client | null>(null);
  const [deleting, setDeleting] = useState(false);

  const errors = useMemo(() => validateClientForm(form), [form]);
  const hasErrors = Object.keys(errors).length > 0;

  const communes = useMemo(() => getCommunes(form.wilaya), [form.wilaya]);
  const officialCodes = useMemo(
    () => getPostalCodes(form.wilaya, form.city),
    [form.wilaya, form.city],
  );

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
    setTouched({});
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
    setTouched({});
    setError('');
    setDialogOpen(true);
  };

  const save = async () => {
    if (hasErrors) return;
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

  const set = (field: keyof ClientForm, value: string) =>
    setForm((f) => ({ ...f, [field]: value }));

  const blur = (field: keyof ClientForm) =>
    setTouched((prev) => ({ ...prev, [field]: true }));

  // Reset de la commune et du code postal quand la wilaya change.
  const onWilayaChange = (code: string) => {
    setForm((f) => ({ ...f, wilaya: code, city: '', postalCode: '' }));
  };

  // Un code postal appartient à une commune : on le vide quand la commune change.
  const onCityChange = (city: string) => {
    setForm((f) => ({ ...f, city, postalCode: '' }));
  };

  const mark = (field: keyof ClientForm, required: boolean): FieldMark => {
    if (!touched[field] && !form[field]) return 'none';
    return markFor(form[field] as string, errors[field], required);
  };

  const fieldError = (field: keyof ClientForm) => (touched[field] ? errors[field] : '');

  const operatorPhone = operatorOf(form.phone);
  const operatorMobile = operatorOf(form.mobile);

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
          inputRef={searchRef}
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
                <TableCell align="right" className="tnum">{c.outstanding !== undefined ? formatCurrency(c.outstanding) : '—'}</TableCell>
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
              required
              value={form.displayName}
              onChange={(e) => set('displayName', e.target.value)}
              onBlur={() => blur('displayName')}
              error={!!fieldError('displayName')}
              helperText={fieldError('displayName') || ' '}
              InputProps={{
                endAdornment: <MarkIcon mark={mark('displayName', true)} />,
              }}
            />
            <TextField select label={t('client.type')} fullWidth value={form.type} onChange={(e) => set('type', e.target.value as ClientType)}>
              {(['Entreprise', 'Particulier', 'ProfessionnelLiberal'] as ClientType[]).map((ty) => (
                <MenuItem key={ty} value={ty}>
                  {t(`client.typeLabels.${ty}`)}
                </MenuItem>
              ))}
            </TextField>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label={t('client.companyName')} fullWidth value={form.companyName} onChange={(e) => set('companyName', e.target.value)} onBlur={() => blur('companyName')} />
              <TextField label={t('client.sector')} fullWidth value={form.sector} onChange={(e) => set('sector', e.target.value)} onBlur={() => blur('sector')} />
            </Box>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField
                label={form.type === 'Entreprise' ? `${t('client.nif')} *` : t('client.nif')}
                fullWidth
                required={form.type === 'Entreprise'}
                value={form.nif}
                onChange={(e) => set('nif', e.target.value)}
                onBlur={() => blur('nif')}
                error={!!fieldError('nif')}
                helperText={fieldError('nif') || ' '}
                inputProps={{ maxLength: 15, inputMode: 'numeric', pattern: '[0-9]*' }}
                InputProps={{ endAdornment: <MarkIcon mark={mark('nif', form.type === 'Entreprise')} /> }}
              />
              <TextField
                label={t('client.rc')}
                fullWidth
                value={form.rc}
                onChange={(e) => set('rc', e.target.value)}
                onBlur={() => blur('rc')}
                error={!!fieldError('rc')}
                helperText={fieldError('rc') || ' '}
                placeholder="16/00-0000000B00"
                InputProps={{ endAdornment: <MarkIcon mark={mark('rc', false)} /> }}
              />
              <TextField
                label={t('client.art')}
                fullWidth
                value={form.art}
                onChange={(e) => set('art', e.target.value)}
                onBlur={() => blur('art')}
                error={!!fieldError('art')}
                helperText={fieldError('art') || ' '}
                inputProps={{ maxLength: 13, inputMode: 'numeric', pattern: '[0-9]*' }}
                InputProps={{ endAdornment: <MarkIcon mark={mark('art', false)} /> }}
              />
            </Box>
            <TextField
              label={t('client.address')}
              fullWidth
              required
              value={form.address}
              onChange={(e) => set('address', e.target.value)}
              onBlur={() => blur('address')}
              error={!!fieldError('address')}
              helperText={fieldError('address') || ' '}
              InputProps={{ endAdornment: <MarkIcon mark={mark('address', true)} /> }}
            />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <FormControl fullWidth sx={{ minWidth: 200 }}>
                <InputLabel>{t('client.wilaya')}</InputLabel>
                <Select
                  label={t('client.wilaya')}
                  value={form.wilaya}
                  onChange={(e) => onWilayaChange(e.target.value as string)}
                  onBlur={() => blur('wilaya')}
                  error={!!fieldError('wilaya')}
                >
                  <MenuItem value="">
                    <em>{t('client.selectWilaya')}</em>
                  </MenuItem>
                  {getWilayas().map((w) => (
                    <MenuItem key={w.code} value={w.code}>
                      {w.code} — {w.nameFr}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <FormControl fullWidth sx={{ minWidth: 220 }}>
                <InputLabel>{t('client.city')}</InputLabel>
                <Select
                  label={t('client.city')}
                  value={form.city}
                  onChange={(e) => onCityChange(e.target.value as string)}
                  onBlur={() => blur('city')}
                  error={!!fieldError('city')}
                  disabled={!form.wilaya}
                >
                  <MenuItem value="">
                    <em>{t('client.selectCity')}</em>
                  </MenuItem>
                  {communes.map((c) => (
                    <MenuItem key={`${c.wilayaCode}-${c.nameFr}`} value={c.nameFr}>
                      {c.nameFr}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Box>
            {fieldError('wilaya') && (
              <Typography color="error" variant="caption">{fieldError('wilaya')}</Typography>
            )}
            {fieldError('city') && (
              <Typography color="error" variant="caption">{fieldError('city')}</Typography>
            )}
            <TextField
              label={t('client.postalCode')}
              fullWidth
              value={form.postalCode}
              onChange={(e) => set('postalCode', e.target.value.replace(/[^\d]/g, '').slice(0, 5))}
              onBlur={() => blur('postalCode')}
              error={!!fieldError('postalCode')}
              helperText={fieldError('postalCode') || ' '}
              inputProps={{ maxLength: 5, inputMode: 'numeric', pattern: '[0-9]*' }}
              InputProps={{ endAdornment: <MarkIcon mark={mark('postalCode', false)} /> }}
            />
            {officialCodes.length > 0 && (
              <Box>
                <Typography variant="caption" color="text.secondary">
                  {t('client.officialCodes')}
                </Typography>
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mt: 0.5 }}>
                  {officialCodes.map((code) => (
                    <Chip
                      key={code}
                      label={code}
                      size="small"
                      variant={form.postalCode === code ? 'filled' : 'outlined'}
                      color={form.postalCode === code ? 'primary' : 'default'}
                      onClick={() => set('postalCode', code)}
                    />
                  ))}
                </Box>
              </Box>
            )}
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField
                label={t('client.phone')}
                fullWidth
                value={form.phone}
                onChange={(e) => set('phone', e.target.value)}
                onBlur={() => blur('phone')}
                error={!!fieldError('phone')}
                helperText={fieldError('phone') || ' '}
                inputProps={{ inputMode: 'tel' }}
                InputProps={{ endAdornment: <MarkIcon mark={mark('phone', false)} /> }}
              />
              <TextField
                label={t('client.mobile')}
                fullWidth
                value={form.mobile}
                onChange={(e) => set('mobile', e.target.value)}
                onBlur={() => blur('mobile')}
                error={!!fieldError('mobile')}
                helperText={fieldError('mobile') || ' '}
                inputProps={{ inputMode: 'tel' }}
                InputProps={{ endAdornment: <MarkIcon mark={mark('mobile', false)} /> }}
              />
              <TextField
                label={t('client.email')}
                fullWidth
                value={form.email}
                onChange={(e) => set('email', e.target.value)}
                onBlur={() => blur('email')}
                error={!!fieldError('email')}
                helperText={fieldError('email') || ' '}
                InputProps={{ endAdornment: <MarkIcon mark={mark('email', false)} /> }}
              />
            </Box>
            {(operatorPhone || operatorMobile) && (
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
                <Typography variant="caption" color="text.secondary">
                  {t('client.operatorPrefix')} :
                </Typography>
                {operatorPhone && <Chip size="small" label={`${form.phone} — ${operatorPhone}`} color="primary" variant="outlined" />}
                {operatorMobile && <Chip size="small" label={`${form.mobile} — ${operatorMobile}`} color="primary" variant="outlined" />}
              </Box>
            )}
            <TextField select label={t('invoice.paymentMethod')} fullWidth value={form.defaultPaymentMethod} onChange={(e) => set('defaultPaymentMethod', e.target.value as PaymentMethod)}>
              {(['Comptant', 'Cheque', 'VirementBancaire', 'CarteBancaire', 'Credit'] as PaymentMethod[]).map((pm) => (
                <MenuItem key={pm} value={pm}>
                  {t(`paymentLabels.${pm}`)}
                </MenuItem>
              ))}
            </TextField>
            <TextField label={t('invoice.notes')} fullWidth multiline rows={2} value={form.notes} onChange={(e) => set('notes', e.target.value)} />
            {hasErrors && (
              <Typography color="warning.main" variant="caption">
                {t('client.formHint')}
              </Typography>
            )}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>{t('common.cancel')}</Button>
          <Button variant="contained" disabled={hasErrors} onClick={() => void save()}>
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
