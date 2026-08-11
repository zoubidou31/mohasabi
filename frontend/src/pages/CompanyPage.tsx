import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Avatar,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Divider,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  TextField,
  Typography,
  useTheme,
} from '@mui/material';
import { Building2, Download, Mail, Phone, RefreshCw, Save, Settings2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { Company, TVARate } from '../api/types';
import PageHeader from '../components/PageHeader';
import { useUpdateStore } from '../stores/updateStore';
import { SHORTCUT_EVENTS, useShortcutEvent } from '../utils/shortcuts';
import {
  validateCompanyForm,
  validateNIF,
  validateNIS,
  validateRC,
  validateART,
  validatePhone,
  validateEmail,
  validateAddress,
  validatePostalCode,
  validateRIB,
  validateCCP,
  validateCompanyName,
  validateInvoicePrefix,
  validateValidityDays,
  validatePaymentConditions,
  validatePenalties,
  detectMobileOperator,
  type MobileOperator,
  type ValidationErrors,
  type CompanyForm,
} from '../utils/companyValidation';
import { WILAYAS, ALGERIAN_BANKS } from '../data/algerianData';

const EMPTY_COMPANY: Company = {
  id: '',
  companyName: '',
  address: '',
  city: '',
  wilaya: '',
  postalCode: '',
  phone: '',
  mobile: '',
  email: '',
  nif: '',
  nis: '',
  rc: '',
  art: '',
  rib: '',
  ccp: '',
  bankName: '',
  invoicePrefix: 'FAC',
  invoiceSerie: '',
  validityDays: 30,
  defaultTvaRate: 'Normal' as TVARate,
  paymentConditions: '',
  penalties: '',
  bankAccountNumber: '',
  useBankersRounding: false,
};

function companyToForm(c: Company): Company {
  return {
    ...EMPTY_COMPANY,
    ...c,
    companyName: c.companyName ?? '',
    address: c.address ?? '',
    city: c.city ?? '',
    wilaya: c.wilaya ?? '',
    postalCode: c.postalCode ?? '',
    phone: c.phone ?? '',
    mobile: c.mobile ?? '',
    email: c.email ?? '',
    nif: c.nif ?? '',
    nis: c.nis ?? '',
    rc: c.rc ?? '',
    art: c.art ?? '',
    rib: c.rib ?? '',
    ccp: c.ccp ?? '',
    bankName: c.bankName ?? '',
    invoicePrefix: c.invoicePrefix ?? 'FAC',
    invoiceSerie: c.invoiceSerie ?? '',
    validityDays: c.validityDays ?? 30,
    paymentConditions: c.paymentConditions ?? '',
    penalties: c.penalties ?? '',
    bankAccountNumber: c.bankAccountNumber ?? '',
  };
}

const operatorColors: Record<MobileOperator, 'success' | 'info' | 'warning' | 'default'> = {
  Ooredoo: 'success',
  Mobilis: 'info',
  Djezzy: 'warning',
  '': 'default',
};

export default function CompanyPage() {
  const { t } = useTranslation();
  const theme = useTheme();
  const setUpdateStore = useUpdateStore((s) => s.setUpdate);
  const resetUpdateStore = useUpdateStore((s) => s.reset);
  const [form, setForm] = useState<Company>(EMPTY_COMPANY);
  const [logoData, setLogoData] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState('');
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [appVersion, setAppVersion] = useState('');
  const [updateState, setUpdateState] = useState<{
    status: 'idle' | 'checking' | 'done' | 'error';
    updateAvailable: boolean;
    latestVersion?: string;
    releaseNotes?: string;
    message?: string;
  }>({ status: 'idle', updateAvailable: false });
  const [installDialogOpen, setInstallDialogOpen] = useState(false);
  const [installing, setInstalling] = useState(false);
  const [installMessage, setInstallMessage] = useState('');
  const [launchAfterUpdate, setLaunchAfterUpdate] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError('');
    try {
      const { data } = await api.get<Company>('/company');
      if (data && data.id) {
        setForm(companyToForm(data));
      } else {
        setForm(EMPTY_COMPANY);
      }
    } catch (err) {
      setLoadError(extractError(err));
      setForm(EMPTY_COMPANY);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    api
      .get<{ version: string }>('/version')
      .then(({ data }) => setAppVersion(data.version))
      .catch(() => setAppVersion(''));
  }, []);

  const checkForUpdates = useCallback(async () => {
    setUpdateState((s) => ({ ...s, status: 'checking' }));
    try {
      const { data } = await api.get<{
        success: boolean;
        updateAvailable: boolean;
        latestVersion?: string;
        releaseNotes?: string;
        message?: string;
      }>('/update/check');
      setUpdateState({
        status: data.success ? 'done' : 'error',
        updateAvailable: data.updateAvailable,
        latestVersion: data.latestVersion,
        releaseNotes: data.releaseNotes,
        message: data.message,
      });
      if (data.success) {
        if (data.updateAvailable) {
          setUpdateStore({
            updateAvailable: true,
            latestVersion: data.latestVersion,
            releaseNotes: data.releaseNotes,
          });
        } else {
          resetUpdateStore();
        }
      }
    } catch (err) {
      setUpdateState({ status: 'error', updateAvailable: false, message: extractError(err) });
    }
  }, [setUpdateStore, resetUpdateStore]);

  const installUpdate = useCallback(async () => {
    setInstalling(true);
    setInstallMessage('');
    try {
      await api.post('/update/install', { launchAfterUpdate });
      setInstallMessage(launchAfterUpdate ? t('update.downloaded') : t('update.downloadedNoRestart'));
      resetUpdateStore();
    } catch (err) {
      setUpdateState({ status: 'error', updateAvailable: true, message: extractError(err) });
    } finally {
      setInstalling(false);
      setInstallDialogOpen(false);
    }
  }, [t, resetUpdateStore, launchAfterUpdate]);

  const onLogoChange = (file: File | undefined) => {
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => setLogoData(reader.result as string);
    reader.readAsDataURL(file);
  };

  const validateSingle = (field: keyof CompanyForm, value: unknown) => {
    const fieldValidators: Record<string, (v: unknown) => string> = {
      companyName: (v) => validateCompanyName(v),
      nif: (v) => validateNIF(v),
      nis: (v) => validateNIS(v),
      rc: (v) => validateRC(v),
      art: (v) => validateART(v),
      phone: (v) => validatePhone(v),
      mobile: (v) => validatePhone(v, false),
      email: (v) => validateEmail(v),
      address: (v) => validateAddress(v),
      postalCode: (v) => validatePostalCode(v, form.wilaya),
      rib: (v) => validateRIB(v),
      ccp: (v) => validateCCP(v),
      invoicePrefix: (v) => validateInvoicePrefix(v),
      validityDays: (v) => validateValidityDays(v),
      paymentConditions: (v) => validatePaymentConditions(v),
      penalties: (v) => validatePenalties(v),
    };
    const validator = fieldValidators[field];
    if (validator) {
      const err = validator(value);
      setErrors((prev) => {
        const next = { ...prev };
        if (err) next[field] = err;
        else delete next[field];
        return next;
      });
    }
  };

  const set = (field: string, value: unknown) => {
    setForm((f) => ({ ...f, [field]: value }));
    setSaved(false);
    if (touched[field]) {
      validateSingle(field as keyof CompanyForm, value);
    }
  };

  const onBlur = (field: string) => {
    setTouched((prev) => ({ ...prev, [field]: true }));
    const val = form[field as keyof Company] ?? '';
    validateSingle(field as keyof CompanyForm, val);
  };

  const save = async () => {
    setError('');
    setSaved(false);

    const formErrors = validateCompanyForm({
      companyName: form.companyName,
      nif: form.nif,
      nis: form.nis,
      rc: form.rc,
      art: form.art,
      phone: form.phone,
      mobile: form.mobile ?? '',
      email: form.email,
      address: form.address,
      postalCode: form.postalCode ?? '',
      rib: form.rib ?? '',
      ccp: form.ccp ?? '',
      bankName: form.bankName ?? '',
      invoicePrefix: form.invoicePrefix,
      validityDays: form.validityDays,
      paymentConditions: form.paymentConditions ?? '',
      penalties: form.penalties ?? '',
      city: form.city ?? '',
      wilaya: form.wilaya ?? '',
    });
    setErrors(formErrors);
    setTouched(
      Object.keys(formErrors).reduce((acc, k) => ({ ...acc, [k]: true }), {} as Record<string, boolean>),
    );

    if (Object.keys(formErrors).length > 0) {
      setError('Veuillez corriger les erreurs avant de sauvegarder.');
      return;
    }

    try {
      const payload = { ...form, logoData };
      const { data } = await api.put<Company>('/company', payload);
      setForm(companyToForm(data));
      setLogoData(null);
      setErrors({});
      setSaved(true);
    } catch (err) {
      setError(extractError(err));
    }
  };

  useShortcutEvent(SHORTCUT_EVENTS.SAVE, () => void save());

  if (loading) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: 300, gap: 2 }}>
        <CircularProgress size={36} />
        <Typography color="text.secondary">Chargement des paramètres...</Typography>
      </Box>
    );
  }

  if (loadError) {
    return (
      <Box sx={{ maxWidth: 900 }} className="animate-fade-up">
        <PageHeader title={t('company.title')} description={t('company.description')} />
        <Alert severity="error" sx={{ mb: 2 }}>
          Impossible de charger les paramètres. Vérifiez que Mohasabi est démarré.
        </Alert>
        <Button variant="outlined" onClick={() => void load()}>Réessayer</Button>
      </Box>
    );
  }

  const mobileOp = detectMobileOperator(form.mobile);
  const errorCount = Object.keys(errors).length;

  const fieldStyle = (_field: string) => ({
    '& .MuiOutlinedInput-root': {
      '&.Mui-error': { borderColor: 'error.main' },
    },
    '& .MuiFormHelperText-root.Mui-error': { color: 'error.main', fontWeight: 600 },
  });

  return (
    <Box sx={{ maxWidth: 1200 }} className="animate-fade-up">
      <PageHeader title={t('company.title')} description={t('company.description')} />

      {saved && (
        <Alert severity="success" sx={{ mb: 2 }}>
          Informations enregistrées avec succès.
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: '1fr 320px' },
          gap: 2,
          alignItems: 'start',
        }}
      >
        <Box sx={{ minWidth: 0 }}>
      {/* ─── Logo ─── */}
      <Card sx={{ mb: 2 }}>
        <CardContent sx={{ p: 3 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 2 }}>
            <Building2 size={20} color={theme.palette.primary.main} />
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              {t('company.logo')}
            </Typography>
          </Box>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <Avatar src={logoData ?? (form.logoPath ? `${api.defaults.baseURL ?? ''}/files/${(form.logoPath ?? '').split('/').pop()}` : undefined)} sx={{ width: 88, height: 88, fontSize: 34, borderRadius: 3 }} variant="rounded">
              {(form.companyName ?? 'F').charAt(0) || 'F'}
            </Avatar>
            <Button component="label" variant="outlined">
              {t('company.logo')}
              <input type="file" accept="image/png,image/jpeg" hidden onChange={(e) => onLogoChange(e.target.files?.[0])} />
            </Button>
          </Box>
        </CardContent>
      </Card>

      {/* ─── Informations entreprise ─── */}
      <Card sx={{ mb: 2 }}>
        <CardContent sx={{ p: 3 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 2 }}>
            <Building2 size={20} color={theme.palette.primary.main} />
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              {t('company.companyName')}
            </Typography>
          </Box>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <TextField
              label={t('company.companyName')}
              fullWidth
              required
              value={form.companyName}
              onChange={(e) => set('companyName', e.target.value)}
              onBlur={() => onBlur('companyName')}
              error={touched.companyName && !!errors.companyName}
              helperText={touched.companyName && errors.companyName}
              sx={fieldStyle('companyName')}
            />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField
                label="NIF (15 chiffres)"
                fullWidth
                required
                value={form.nif}
                onChange={(e) => set('nif', e.target.value.replace(/[^\d]/g, '').slice(0, 15))}
                onBlur={() => onBlur('nif')}
                error={touched.nif && !!errors.nif}
                helperText={touched.nif && errors.nif}
                inputProps={{ maxLength: 15, inputMode: 'numeric', pattern: '[0-9]*' }}
                sx={fieldStyle('nif')}
              />
              <TextField
                label="NIS (15 chiffres)"
                fullWidth
                required
                value={form.nis}
                onChange={(e) => set('nis', e.target.value.replace(/[^\d]/g, '').slice(0, 15))}
                onBlur={() => onBlur('nis')}
                error={touched.nis && !!errors.nis}
                helperText={touched.nis && errors.nis}
                inputProps={{ maxLength: 15, inputMode: 'numeric', pattern: '[0-9]*' }}
                sx={fieldStyle('nis')}
              />
              <TextField
                label="RC (16/00-0000000B00)"
                fullWidth
                required
                value={form.rc}
                onChange={(e) => set('rc', e.target.value.toUpperCase().replace(/[^A-Z0-9/\-]/g, '').slice(0, 16))}
                onBlur={() => onBlur('rc')}
                error={touched.rc && !!errors.rc}
                helperText={touched.rc && errors.rc}
                inputProps={{ maxLength: 16 }}
                sx={fieldStyle('rc')}
              />
              <TextField
                label="ART (13 chiffres)"
                fullWidth
                required
                value={form.art}
                onChange={(e) => set('art', e.target.value.replace(/[^\d]/g, '').slice(0, 13))}
                onBlur={() => onBlur('art')}
                error={touched.art && !!errors.art}
                helperText={touched.art && errors.art}
                inputProps={{ maxLength: 13, inputMode: 'numeric', pattern: '[0-9]*' }}
                sx={fieldStyle('art')}
              />
            </Box>
            <TextField
              label={t('company.address')}
              fullWidth
              required
              value={form.address}
              onChange={(e) => set('address', e.target.value)}
              onBlur={() => onBlur('address')}
              error={touched.address && !!errors.address}
              helperText={touched.address && errors.address}
              sx={fieldStyle('address')}
            />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label={t('company.city')} fullWidth value={form.city} onChange={(e) => set('city', e.target.value)} />
              <FormControl fullWidth>
                <InputLabel>{t('company.wilaya')}</InputLabel>
                <Select
                  label={t('company.wilaya')}
                  value={form.wilaya}
                  onChange={(e) => {
                    const newWilaya = e.target.value;
                    set('wilaya', newWilaya);
                    // Reset postal code — clear suffix, keep only prefix when wilaya selected
                    set('postalCode', newWilaya ?? '');
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
                  // The input only ever holds the 3-digit suffix; the wilaya prefix is locked above.
                  const raw = e.target.value.replace(/[^\d]/g, '');
                  const suffix = raw.slice(-3);
                  set('postalCode', form.wilaya + suffix);
                }}
                onBlur={() => onBlur('postalCode')}
                error={touched.postalCode && !!errors.postalCode}
                helperText={touched.postalCode && errors.postalCode}
                InputProps={{
                  startAdornment: form.wilaya ? (
                    <Typography
                      component="span"
                      sx={{
                        color: 'text.secondary',
                        fontWeight: 700,
                        mr: 0.5,
                        userSelect: 'none',
                        bgcolor: 'grey.100',
                        px: 0.75,
                        py: 0.5,
                        borderRadius: 1,
                        border: '1px solid',
                        borderColor: 'grey.300',
                      }}
                    >
                      {form.wilaya}
                    </Typography>
                  ) : null,
                }}
                inputProps={{
                  maxLength: form.wilaya ? 3 : 5,
                  inputMode: 'numeric',
                  pattern: '[0-9]*',
                }}
                disabled={!form.wilaya}
                sx={fieldStyle('postalCode')}
              />
            </Box>
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: '1fr 1fr 1.4fr' },
                gap: 2,
              }}
            >
              <Box>
                <TextField
                  label={t('company.phone')}
                  fullWidth
                  required
                  value={form.phone}
                  onChange={(e) => set('phone', e.target.value.replace(/[^\d]/g, '').slice(0, 10))}
                  onBlur={() => onBlur('phone')}
                  error={touched.phone && !!errors.phone}
                  helperText={touched.phone && errors.phone}
                  inputProps={{ maxLength: 10, inputMode: 'numeric', pattern: '[0-9]*' }}
                  sx={fieldStyle('phone')}
                />
              </Box>
              <Box>
                <TextField
                  label={t('company.mobile')}
                  fullWidth
                  value={form.mobile}
                  onChange={(e) => set('mobile', e.target.value.replace(/[^\d]/g, '').slice(0, 10))}
                  onBlur={() => onBlur('mobile')}
                  error={touched.mobile && !!errors.mobile}
                  helperText={touched.mobile && errors.mobile}
                  inputProps={{ maxLength: 10, inputMode: 'numeric', pattern: '[0-9]*' }}
                  sx={fieldStyle('mobile')}
                />
                {mobileOp && !errors.mobile && (form.mobile ?? '').length >= 9 && (
                  <Chip
                    label={mobileOp}
                    size="small"
                    color={operatorColors[mobileOp]}
                    sx={{ mt: 0.5, fontWeight: 600 }}
                  />
                )}
              </Box>
              <Box>
                <TextField
                  label={t('company.email')}
                  fullWidth
                  required
                  value={form.email}
                  onChange={(e) => set('email', e.target.value)}
                  onBlur={() => onBlur('email')}
                  error={touched.email && !!errors.email}
                  helperText={touched.email && errors.email}
                  sx={fieldStyle('email')}
                />
              </Box>
            </Box>
          </Box>
        </CardContent>
      </Card>

      {/* ─── Paramètres facturation ─── */}
      <Card sx={{ mb: 2 }}>
        <CardContent sx={{ p: 3 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 2 }}>
            <Settings2 size={20} color={theme.palette.primary.main} />
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              {t('nav.settings')}
            </Typography>
          </Box>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField
                label={t('company.invoicePrefix')}
                fullWidth
                required
                value={form.invoicePrefix}
                onChange={(e) => set('invoicePrefix', e.target.value.toUpperCase().replace(/[^A-Z0-9\-]/g, ''))}
                onBlur={() => onBlur('invoicePrefix')}
                error={touched.invoicePrefix && !!errors.invoicePrefix}
                helperText={touched.invoicePrefix && errors.invoicePrefix}
                sx={fieldStyle('invoicePrefix')}
              />
              <TextField label={t('company.invoiceSerie')} fullWidth value={form.invoiceSerie} onChange={(e) => set('invoiceSerie', e.target.value)} />
              <TextField
                label={t('company.validityDays')}
                type="number"
                fullWidth
                value={form.validityDays}
                onChange={(e) => set('validityDays', parseInt(e.target.value, 10) || 0)}
                onBlur={() => onBlur('validityDays')}
                error={touched.validityDays && !!errors.validityDays}
                helperText={touched.validityDays && errors.validityDays}
                inputProps={{ min: 0, max: 365 }}
                sx={fieldStyle('validityDays')}
              />
            </Box>
            <FormControl fullWidth>
              <InputLabel>{t('company.defaultTvaRate')}</InputLabel>
              <Select label={t('company.defaultTvaRate')} value={form.defaultTvaRate} onChange={(e) => set('defaultTvaRate', e.target.value as TVARate)}>
                {(['Normal', 'Reduit', 'Exonere', 'IFU'] as TVARate[]).map((rate) => (
                  <MenuItem key={rate} value={rate}>
                    {t(`tvaLabels.${rate}`)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField
                label="RIB (20 chiffres)"
                fullWidth
                value={form.rib}
                onChange={(e) => set('rib', e.target.value.replace(/[^\d]/g, '').slice(0, 20))}
                onBlur={() => onBlur('rib')}
                error={touched.rib && !!errors.rib}
                helperText={touched.rib && errors.rib}
                inputProps={{ maxLength: 20, inputMode: 'numeric', pattern: '[0-9]*' }}
                sx={fieldStyle('rib')}
              />
              <TextField
                label="CCP"
                fullWidth
                value={form.ccp}
                onChange={(e) => set('ccp', e.target.value.replace(/[^\d]/g, '').slice(0, 12))}
                onBlur={() => onBlur('ccp')}
                error={touched.ccp && !!errors.ccp}
                helperText={touched.ccp && errors.ccp}
                inputProps={{ maxLength: 12, inputMode: 'numeric', pattern: '[0-9]*' }}
                sx={fieldStyle('ccp')}
              />
              <FormControl fullWidth>
                <InputLabel>{t('company.bankName')}</InputLabel>
                <Select
                  label={t('company.bankName')}
                  value={form.bankName}
                  onChange={(e) => set('bankName', e.target.value)}
                >
                  <MenuItem value="">
                    <em>Aucune</em>
                  </MenuItem>
                  {ALGERIAN_BANKS.map((bank) => (
                    <MenuItem key={bank} value={bank}>
                      {bank}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Box>
            <TextField label={t('company.bankAccountNumber')} fullWidth value={form.bankAccountNumber} onChange={(e) => set('bankAccountNumber', e.target.value)} />
            <TextField
              label={t('company.paymentConditions')}
              fullWidth
              required
              multiline
              rows={2}
              value={form.paymentConditions}
              onChange={(e) => set('paymentConditions', e.target.value)}
              onBlur={() => onBlur('paymentConditions')}
              error={touched.paymentConditions && !!errors.paymentConditions}
              helperText={touched.paymentConditions && errors.paymentConditions}
              sx={fieldStyle('paymentConditions')}
            />
            <TextField
              label={`${t('company.penalties')} (ex : 0.5% par mois)`}
              fullWidth
              multiline
              rows={2}
              value={form.penalties}
              onChange={(e) => set('penalties', e.target.value)}
              onBlur={() => onBlur('penalties')}
              error={touched.penalties && !!errors.penalties}
              helperText={touched.penalties && errors.penalties}
              sx={fieldStyle('penalties')}
            />
            <FormControlLabel
              control={<Checkbox checked={form.useBankersRounding} onChange={(e) => set('useBankersRounding', e.target.checked)} />}
              label={t('company.useBankersRounding')}
            />
          </Box>
        </CardContent>
      </Card>

      {/* ─── Sauvegarder ─── */}
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', gap: 2 }}>
        {errorCount > 0 && (
          <Typography variant="body2" color="error" sx={{ fontWeight: 600 }}>
            {errorCount} erreur{errorCount > 1 ? 's' : ''} à corriger
          </Typography>
        )}
        <Button variant="contained" startIcon={<Save size={18} />} onClick={() => void save()}>
          {t('common.save')}
        </Button>
      </Box>
        </Box>

        {/* ─── À propos / Marque ─── */}
        <Box sx={{ position: { md: 'sticky' }, top: { md: 104 } }}>
          <Card>
            <CardContent
              sx={{ p: 3, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1.25 }}
            >
              <Box
                component="img"
                src="/mohasabi.png"
                alt={t('appName')}
                sx={{ width: 120, height: 120, objectFit: 'contain', borderRadius: 3 }}
              />
              <Typography sx={{ fontWeight: 800, fontSize: 22, letterSpacing: '-0.01em' }}>
                {t('appName')}
              </Typography>
              <Typography sx={{ fontWeight: 500, fontSize: 13, color: 'text.secondary' }}>
                {t('appSubtitle')}
              </Typography>
              {appVersion && (
              <Chip
                label={`${t('about.version')} ${appVersion}`}
                size="small"
                sx={{ mt: 0.5, fontWeight: 600, bgcolor: 'primary.light', color: 'primary.main' }}
              />
            )}
              <Divider flexItem sx={{ my: 1 }} />
              <Box sx={{ width: '100%', display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                  <Avatar sx={{ width: 40, height: 40, bgcolor: 'primary.main', fontSize: 16, fontWeight: 700 }}>
                    {t('update.author').charAt(0)}
                  </Avatar>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography sx={{ fontSize: 12, color: 'text.secondary', fontWeight: 600 }}>
                      {t('update.contactTitle')}
                    </Typography>
                    <Typography sx={{ fontWeight: 700, fontSize: 15 }}>{t('update.author')}</Typography>
                  </Box>
                </Box>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, color: 'text.secondary' }}>
                  <Mail size={17} color={theme.palette.primary.main} style={{ flexShrink: 0 }} />
                  <Box sx={{ minWidth: 0 }}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{t('update.email')}</Typography>
                    <Typography sx={{ fontSize: 13.5, color: 'text.primary', fontWeight: 600, wordBreak: 'break-all' }}>
                      mohzoubid@gmail.com
                    </Typography>
                  </Box>
                </Box>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, color: 'text.secondary' }}>
                  <Phone size={17} color={theme.palette.primary.main} style={{ flexShrink: 0 }} />
                  <Box sx={{ minWidth: 0 }}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{t('update.phone')}</Typography>
                    <Typography sx={{ fontSize: 13.5, color: 'text.primary', fontWeight: 600 }}>0674947157</Typography>
                  </Box>
                </Box>
              </Box>

              <Divider flexItem sx={{ my: 1 }} />

              {/* ─── Mise à jour ─── */}
              <Box sx={{ width: '100%', display: 'flex', flexDirection: 'column', gap: 1.25 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <RefreshCw size={18} color={theme.palette.primary.main} />
                    <Typography sx={{ fontWeight: 700, fontSize: 15 }}>{t('update.title')}</Typography>
                  </Box>
                  {appVersion && (
                    <Chip
                      label={`${t('update.currentVersion')} : ${appVersion}`}
                      size="small"
                      sx={{ fontWeight: 600, fontSize: 12 }}
                    />
                  )}
                </Box>
                {updateState.status === 'checking' && (
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                    <CircularProgress size={18} />
                    <Typography color="text.secondary" sx={{ fontSize: 13 }}>{t('update.checking')}</Typography>
                  </Box>
                )}
                {installing && (
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                    <CircularProgress size={18} />
                    <Typography color="text.secondary" sx={{ fontSize: 13 }}>{t('update.installing')}</Typography>
                  </Box>
                )}
                {updateState.status === 'done' && updateState.updateAvailable && (
                  <Alert severity="info" sx={{ py: 0.75, '& .MuiAlert-message': { fontSize: 13 } }}>
                    <Box>
                      <Typography sx={{ fontWeight: 700, fontSize: 13 }}>
                        {t('update.available', { version: updateState.latestVersion })}
                      </Typography>
                      {updateState.releaseNotes && (
                        <Typography
                          variant="body2"
                          sx={{ fontSize: 12.5, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}
                        >
                          {updateState.releaseNotes}
                        </Typography>
                      )}
                    </Box>
                  </Alert>
                )}
                {updateState.status === 'done' && !updateState.updateAvailable && (
                  <Alert severity="success" sx={{ py: 0.75, '& .MuiAlert-message': { fontSize: 13 } }}>
                    {t('update.upToDate')}
                  </Alert>
                )}
                {updateState.status === 'error' && (
                  <Alert severity="error" sx={{ py: 0.75, '& .MuiAlert-message': { fontSize: 13 } }}>
                    {updateState.message ?? t('update.checkFailed')}
                  </Alert>
                )}
                {installMessage && (
                  <Alert severity="success" sx={{ py: 0.75, '& .MuiAlert-message': { fontSize: 13 } }}>
                    {installMessage}
                  </Alert>
                )}
                <Button
                  fullWidth
                  variant="outlined"
                  startIcon={<RefreshCw size={16} />}
                  disabled={updateState.status === 'checking' || installing}
                  onClick={() => void checkForUpdates()}
                >
                  {t('update.checkForUpdates')}
                </Button>
                {updateState.status === 'done' && updateState.updateAvailable && (
                  <Button
                    fullWidth
                    variant="contained"
                    startIcon={<Download size={16} />}
                    disabled={installing}
                    onClick={() => setInstallDialogOpen(true)}
                  >
                    {t('update.installNow')}
                  </Button>
                )}
              </Box>
            </CardContent>
          </Card>
        </Box>
      </Box>

      <Dialog open={installDialogOpen} onClose={() => setInstallDialogOpen(false)}>
        <DialogTitle>{t('update.installTitle')}</DialogTitle>
        <DialogContent>
          <DialogContentText sx={{ mb: 2 }}>{t('update.installBody')}</DialogContentText>
          <FormControlLabel
            control={
              <Checkbox
                checked={launchAfterUpdate}
                onChange={(e) => setLaunchAfterUpdate(e.target.checked)}
              />
            }
            label={t('update.installLaunchLabel')}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setInstallDialogOpen(false)}>{t('common.cancel')}</Button>
          <Button variant="contained" disabled={installing} onClick={() => void installUpdate()}>
            {t('update.installConfirm')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
