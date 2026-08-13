import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
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
  IconButton,
  InputAdornment,
  MenuItem,
  Select,
  Snackbar,
  Switch,
  TextField,
  Typography,
  useTheme,
} from '@mui/material';
import {
  Building2,
  Database,
  FolderOpen,
  HardDrive,
  type LucideIcon,
  MonitorCog,
  Palette,
  Save,
  Search,
  ShieldCheck,
  Trash2,
  Type,
  Undo2,
  X,
} from 'lucide-react';
import { useNavigate, useBlocker } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { AppSettings, BackupInfo, BackupRunResult, BackupStatus, RestoreResult } from '../api/types';
import { FONT_FAMILIES, INTERFACE_FONT_SIZES } from '../api/types';
import { formatDateTime, formatNumber } from '../utils/format';
import { useSettingsStore } from '../stores/settingsStore';
import { COMMAND_IDS, useCommand } from '../utils/shortcuts';
import KeyboardShortcuts from '../components/KeyboardShortcuts';
import { useShortcutsStore, DEFAULT_BINDINGS } from '../stores/shortcutsStore';
import type { Bindings, CommandId, ShortcutBinding } from '../stores/shortcutsStore';
import { COMMAND_META } from '../utils/commandMeta';
import InvoicePreview from '../components/InvoicePreview';

const frequencyOptions = [5, 15, 30, 60, 360, 1440];
const retentionOptions = [0, 3, 5, 10];
const sizeOptions = [8, 9, 10, 11, 12, 13, 14, 16, 18];

function Vis({ show, children }: { show: boolean; children: React.ReactNode }) {
  return show ? <>{children}</> : null;
}

const interfaceSizeValues = Object.keys(INTERFACE_FONT_SIZES) as Array<keyof typeof INTERFACE_FONT_SIZES>;

export default function OptionsPage() {
  const { t } = useTranslation();
  const theme = useTheme();
  const navigate = useNavigate();

  const settings = useSettingsStore((s) => s.settings);
  const saveSettings = useSettingsStore((s) => s.save);
  const restartApp = useSettingsStore((s) => s.restartApp);

  const [draft, setDraft] = useState<AppSettings | null>(settings);
  const [draftBindings, setDraftBindings] = useState<Bindings>(() => ({ ...useShortcutsStore.getState().bindings }));
  const [savedBindings, setSavedBindings] = useState<Bindings>(() => ({ ...useShortcutsStore.getState().bindings }));

  const [saving, setSaving] = useState(false);
  const [snack, setSnack] = useState<{ severity: 'success' | 'error'; message: string } | null>(null);

  const [query, setQuery] = useState('');
  const searchRef = useRef<HTMLInputElement>(null);

  const [status, setStatus] = useState<BackupStatus | null>(null);
  const [backups, setBackups] = useState<BackupInfo[]>([]);
  const [backingUp, setBackingUp] = useState(false);

  const [restoreTarget, setRestoreTarget] = useState<BackupInfo | null>(null);
  const [restoring, setRestoring] = useState(false);
  const [restoreMessage, setRestoreMessage] = useState('');
  const [restarting, setRestarting] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<BackupInfo | null>(null);
  const [deleting, setDeleting] = useState(false);

  const [unsavedOpen, setUnsavedOpen] = useState(false);

  useEffect(() => {
    if (settings) setDraft(settings);
  }, [settings]);

  const refresh = useCallback(async () => {
    try {
      const [{ data: st }, { data: list }] = await Promise.all([
        api.get<BackupStatus>('/backup/status'),
        api.get<BackupInfo[]>('/backup/list'),
      ]);
      setStatus(st);
      setBackups(list);
    } catch {
      // Ignoré.
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const settingsDirty = useMemo(
    () => !!settings && !!draft && JSON.stringify(settings) !== JSON.stringify(draft),
    [settings, draft],
  );
  const bindingsDirty = useMemo(
    () => JSON.stringify(savedBindings) !== JSON.stringify(draftBindings),
    [savedBindings, draftBindings],
  );
  const dirty = settingsDirty || bindingsDirty;

  const labelById = useMemo(
    () => Object.fromEntries(COMMAND_META.map((c) => [c.id, c.labelKey])) as Record<CommandId, string>,
    [],
  );

  const blocker = useBlocker(dirty);
  useEffect(() => {
    if (blocker.state === 'blocked') setUnsavedOpen(true);
  }, [blocker.state]);

  const set = <K extends keyof AppSettings>(key: K, value: AppSettings[K]) => {
    setDraft((d) => (d ? { ...d, [key]: value } : d));
  };

  const setDraftBinding = (id: CommandId, b: ShortcutBinding) =>
    setDraftBindings((prev) => ({ ...prev, [id]: b }));
  const resetDraftBinding = (id: CommandId) =>
    setDraftBindings((prev) => ({ ...prev, [id]: { ...DEFAULT_BINDINGS[id] } }));
  const restoreDefaults = () => setDraftBindings({ ...DEFAULT_BINDINGS });

  const trySave = async (): Promise<boolean> => {
    if (!draft) return false;
    setSaving(true);
    setSnack(null);
    try {
      await saveSettings(draft);
      useShortcutsStore.getState().commit(draftBindings);
      setSavedBindings({ ...draftBindings });
      setSnack({ severity: 'success', message: t('options.saveSuccess') });
      return true;
    } catch (err) {
      setSnack({ severity: 'error', message: t('options.saveError') + ' : ' + extractError(err) });
      return false;
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    if (settings) setDraft(settings);
    setDraftBindings({ ...savedBindings });
    setSnack(null);
  };

  useCommand(COMMAND_IDS.SAVE, () => {
    if (dirty) void trySave();
  });
  useCommand(COMMAND_IDS.FOCUS_SEARCH, () => {
    searchRef.current?.focus();
    searchRef.current?.select();
  });

  const onUnsavedSave = async () => {
    setUnsavedOpen(false);
    const ok = await trySave();
    if (ok) blocker.proceed?.();
    else blocker.reset?.();
  };
  const onUnsavedDiscard = () => {
    setUnsavedOpen(false);
    handleCancel();
    blocker.proceed?.();
  };
  const onUnsavedCancel = () => {
    setUnsavedOpen(false);
    blocker.reset?.();
  };

  const handleBackupNow = async () => {
    setBackingUp(true);
    setSnack(null);
    try {
      const { data } = await api.post<BackupRunResult>('/backup/now');
      if (data.success) {
        setSnack({ severity: 'success', message: t('options.dataBackup.lastBackupOk') });
      } else {
        setSnack({ severity: 'error', message: data.error ?? t('options.dataBackup.lastBackupFailed') });
      }
    } catch (err) {
      setSnack({ severity: 'error', message: extractError(err) });
    } finally {
      setBackingUp(false);
      void refresh();
    }
  };

  const handleOpenFolder = async () => {
    try {
      await api.post('/app/open-folder');
    } catch {
      // Ignoré.
    }
  };

  const confirmRestore = (backup: BackupInfo) => setRestoreTarget(backup);

  const executeRestore = async () => {
    if (!restoreTarget || restoring) return;
    setRestoring(true);
    setSnack(null);
    try {
      const { data } = await api.post<RestoreResult>('/restore', { fileName: restoreTarget.fileName });
      if (data.success) {
        setRestoreTarget(null);
        setRestoreMessage(data.message ?? t('options.dataBackup.restoreReady'));
        if (data.requiresRestart) {
          void restartApp().then(() => setRestarting(true));
        }
      } else {
        setSnack({ severity: 'error', message: data.error ?? t('options.dataBackup.restoreError') });
        setRestoreTarget(null);
      }
    } catch (err) {
      setSnack({ severity: 'error', message: extractError(err) });
      setRestoreTarget(null);
    } finally {
      setRestoring(false);
    }
  };

  const confirmDelete = (backup: BackupInfo) => setDeleteTarget(backup);

  const executeDelete = async () => {
    if (!deleteTarget || deleting) return;
    setDeleting(true);
    try {
      await api.delete(`/backup/${encodeURIComponent(deleteTarget.fileName)}`);
    } catch (err) {
      setSnack({ severity: 'error', message: extractError(err) });
    } finally {
      setDeleting(false);
      setDeleteTarget(null);
      void refresh();
    }
  };

  const lastBackupText = status?.lastBackupAt ? formatDateTime(status.lastBackupAt) : t('options.dataBackup.lastBackupNever');
  const lastBackupOk = status?.lastBackupStatus !== 'failed';

  // --- Recherche intelligente ---
  const q = query.trim().toLowerCase();
  const m = (...texts: string[]) => !q || texts.some((tx) => tx.toLowerCase().includes(q));

  const companyMatch = m(t('options.company.title'), t('options.company.description'), 'company', 'société', 'edit', 'modifier');
  const generalMatch = m(t('options.general.title'), t('options.general.description'), 'language', 'langue', 'theme', 'thème');
  const backupMatch = m(
    t('options.dataBackup.title'),
    t('options.dataBackup.description'),
    'backup',
    'sauvegarde',
    'frequency',
    'fréquence',
    'retention',
    'conservation',
    'location',
    'emplacement',
    'folder',
    'dossier',
    'restore',
    'restaurer',
    'delete',
    'supprimer',
  );
  const displayMatch = m(t('options.display.title'), t('options.display.description'), 'splash', 'écran', 'display', 'affichage');
  const typographyMatch = m(
    t('options.typography.title'),
    t('options.typography.description'),
    'typography',
    'typographie',
    'police',
    'font',
    'caractère',
    'invoice',
    'facture',
    'export',
    'document',
    'taille',
    'size',
  );
  const keyboardMatch = m(
    'shortcut',
    'keyboard',
    'raccourci',
    'clavier',
    'touche',
    'key',
    t('options.keyboard.centerTitle'),
  );

  const SectionCard = ({
    icon: Icon,
    title,
    subtitle,
    children,
  }: {
    icon: LucideIcon;
    title: string;
    subtitle: string;
    children: React.ReactNode;
  }) => (
    <Card variant="outlined" sx={{ borderColor: 'divider' }}>
      <CardContent sx={{ p: 1.5 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.25 }}>
          <Icon size={16} style={{ color: theme.palette.primary.main }} />
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            {title}
          </Typography>
        </Box>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
          {subtitle}
        </Typography>
        {children}
      </CardContent>
    </Card>
  );

  return (
    <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column', overflow: { xs: 'auto', md: 'hidden' } }}>
      {/* Header */}
      <Box
        sx={{
          flexShrink: 0,
          display: 'flex',
          alignItems: { xs: 'flex-start', md: 'center' },
          justifyContent: 'space-between',
          gap: 2,
          flexWrap: 'wrap',
          mb: 1.25,
        }}
      >
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: '-0.01em' }}>
            {t('options.title')}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t('options.description')}
          </Typography>
        </Box>
        <TextField
          inputRef={searchRef}
          size="small"
          placeholder={t('options.search')}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          sx={{ minWidth: { xs: '100%', sm: 280 } }}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <Search size={16} />
              </InputAdornment>
            ),
            endAdornment: query ? (
              <InputAdornment position="end">
                <IconButton size="small" onClick={() => setQuery('')} aria-label={t('common.cancel')}>
                  <X size={14} />
                </IconButton>
              </InputAdornment>
            ) : null,
          }}
          inputProps={{ 'aria-label': t('options.searchAria') }}
        />
      </Box>

      {/* Main grid */}
      <Box
        sx={{
          flex: 1,
          minHeight: 0,
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: 'minmax(0, 1fr) minmax(380px, 0.85fr)' },
          gap: 1.5,
          overflow: 'hidden',
        }}
      >
        {/* LEFT — Program settings */}
        <Box sx={{ minHeight: 0, overflowY: 'auto', pr: 0.5, display: 'flex', flexDirection: 'column', gap: 1.25 }}>
          <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.5 }}>
            {t('options.columns.program')}
          </Typography>

          <Vis show={companyMatch}>
            <SectionCard icon={Building2} title={t('options.company.title')} subtitle={t('options.company.description')}>
              <Button variant="outlined" size="small" onClick={() => navigate('/settings')}>
                {t('options.company.open')}
              </Button>
            </SectionCard>
          </Vis>

          <Vis show={generalMatch}>
            <SectionCard icon={Palette} title={t('options.general.title')} subtitle={t('options.general.description')}>
              <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
                <FormControl sx={{ minWidth: 180 }}>
                  <Select size="small" value={draft?.language ?? 'fr'} onChange={(e) => set('language', e.target.value)}>
                    <MenuItem value="fr">Français</MenuItem>
                    <MenuItem value="en">English</MenuItem>
                  </Select>
                </FormControl>
                <FormControl sx={{ minWidth: 180 }}>
                  <Select size="small" value={draft?.theme ?? 'light'} onChange={(e) => set('theme', e.target.value)}>
                    <MenuItem value="light">{t('options.general.themeLight')}</MenuItem>
                    <MenuItem value="dark">{t('options.general.themeDark')}</MenuItem>
                    <MenuItem value="system">{t('options.general.themeSystem')}</MenuItem>
                  </Select>
                </FormControl>
              </Box>
            </SectionCard>
          </Vis>

          <Vis show={backupMatch}>
            <SectionCard icon={ShieldCheck} title={t('options.dataBackup.title')} subtitle={t('options.dataBackup.description')}>
              <FormControlLabel
                control={
                  <Switch
                    size="small"
                    checked={draft?.autoBackupEnabled ?? true}
                    onChange={(e) => set('autoBackupEnabled', e.target.checked)}
                  />
                }
                label={t('options.dataBackup.autoBackup')}
              />
              <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', mt: 1 }}>
                <FormControl sx={{ minWidth: 180 }} disabled={!(draft?.autoBackupEnabled ?? true)}>
                  <Select
                    size="small"
                    value={draft?.backupFrequencyMinutes ?? 30}
                    onChange={(e) => set('backupFrequencyMinutes', Number(e.target.value))}
                  >
                    {frequencyOptions.map((f) => (
                      <MenuItem key={f} value={f}>
                        {t(`options.dataBackup.frequency${f}`)}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl sx={{ minWidth: 160 }}>
                  <Select
                    size="small"
                    value={draft?.backupRetentionCount ?? 5}
                    onChange={(e) => set('backupRetentionCount', Number(e.target.value))}
                  >
                    {retentionOptions.map((r) => (
                      <MenuItem key={r} value={r}>
                        {t(`options.dataBackup.retention${r}`)}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </Box>

              <Divider sx={{ my: 1.25 }} />

              <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, minWidth: 0 }}>
                  <HardDrive size={16} style={{ color: 'text.secondary', flexShrink: 0 }} />
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {t('options.dataBackup.backupLocation')}
                    </Typography>
                    <Typography variant="caption" color="text.secondary" sx={{ wordBreak: 'break-all', display: 'block' }}>
                      {draft?.backupLocation || '—'}
                    </Typography>
                  </Box>
                </Box>
                <Box sx={{ display: 'flex', gap: 1 }}>
                  <Button variant="outlined" size="small" startIcon={<FolderOpen size={14} />} onClick={() => void handleOpenFolder()}>
                    {t('options.dataBackup.openFolder')}
                  </Button>
                  <Button
                    variant="contained"
                    size="small"
                    startIcon={backingUp ? <CircularProgress size={14} color="inherit" /> : <Database size={14} />}
                    disabled={backingUp}
                    onClick={() => void handleBackupNow()}
                  >
                    {backingUp ? t('options.dataBackup.backupRunning') : t('options.dataBackup.backupNow')}
                  </Button>
                </Box>
              </Box>

              <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mt: 1.25, alignItems: 'center' }}>
                <Typography variant="caption" sx={{ fontWeight: 600 }}>
                  {t('options.dataBackup.lastBackup')} :
                </Typography>
                <Chip
                  size="small"
                  color={lastBackupOk ? 'success' : 'error'}
                  variant="outlined"
                  label={`${lastBackupText} — ${lastBackupOk ? t('options.dataBackup.lastBackupOk') : t('options.dataBackup.lastBackupFailed')}`}
                />
                <Chip size="small" variant="outlined" label={t('options.dataBackup.backupsCount', { count: status?.backupCount ?? 0 })} />
                <Chip
                  size="small"
                  variant="outlined"
                  label={`${t('options.dataBackup.totalSize')} : ${formatNumber((status?.totalSize ?? 0) / 1024 / 1024)} Mo`}
                />
              </Box>

              <Typography variant="caption" sx={{ fontWeight: 700, display: 'block', mt: 1.25, color: 'text.secondary' }}>
                {t('options.dataBackup.listTitle')}
              </Typography>
              <Box sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 1, mt: 0.5, maxHeight: 120, overflowY: 'auto' }}>
                {backups.length === 0 ? (
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', p: 1.5, textAlign: 'center' }}>
                    {t('options.dataBackup.empty')}
                  </Typography>
                ) : (
                  backups.map((b) => (
                    <Box
                      key={b.fileName}
                      sx={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        gap: 1,
                        px: 1.5,
                        py: 0.75,
                        borderBottom: '1px solid',
                        borderColor: 'divider',
                        '&:last-of-type': { borderBottom: 0 },
                      }}
                    >
                      <Box sx={{ minWidth: 0 }}>
                        <Typography variant="body2" sx={{ fontWeight: 600, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                          {b.fileName}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {formatDateTime(b.createdAt)} · {formatNumber(b.size / 1024 / 1024)} Mo
                        </Typography>
                      </Box>
                      <Box sx={{ display: 'inline-flex', gap: 0.5, flexShrink: 0 }}>
                        <Button size="small" variant="outlined" startIcon={<Undo2 size={13} />} onClick={() => confirmRestore(b)}>
                          {t('options.dataBackup.restore')}
                        </Button>
                        <IconButton size="small" color="error" onClick={() => confirmDelete(b)} aria-label={t('options.dataBackup.delete')}>
                          <Trash2 size={14} />
                        </IconButton>
                      </Box>
                    </Box>
                  ))
                )}
              </Box>
            </SectionCard>
          </Vis>

          <Vis show={displayMatch}>
            <SectionCard icon={MonitorCog} title={t('options.display.title')} subtitle={t('options.display.description')}>
              <FormControlLabel
                control={
                  <Switch
                    size="small"
                    checked={draft?.splashEnabled ?? true}
                    onChange={(e) => set('splashEnabled', e.target.checked)}
                  />
                }
                label={t('options.display.splash')}
              />
            </SectionCard>
          </Vis>

          <Vis show={typographyMatch}>
            <SectionCard icon={Type} title={t('options.typography.title')} subtitle={t('options.typography.description')}>
              <Typography variant="caption" sx={{ fontWeight: 700, display: 'block', mt: 0.5 }}>
                {t('options.typography.interfaceTitle')}
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.75 }}>
                {t('options.typography.interfaceDesc')}
              </Typography>
              <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', mb: 1 }}>
                <FormControl sx={{ minWidth: 200 }}>
                  <Select
                    size="small"
                    value={draft?.appFontFamily ?? 'Inter'}
                    onChange={(e) => set('appFontFamily', e.target.value)}
                  >
                    {FONT_FAMILIES.map((f) => (
                      <MenuItem key={f} value={f}>
                        {f}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl sx={{ minWidth: 140 }}>
                  <Select
                    size="small"
                    value={draft?.interfaceFontSize ?? 'medium'}
                    onChange={(e) => set('interfaceFontSize', e.target.value as AppSettings['interfaceFontSize'])}
                  >
                    {interfaceSizeValues.map((s) => (
                      <MenuItem key={s} value={s}>
                        {t(`options.typography.size${s[0].toUpperCase()}${s.slice(1)}`)}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </Box>

              <Divider sx={{ my: 1 }} />

              <Typography variant="caption" sx={{ fontWeight: 700, display: 'block' }}>
                {t('options.typography.docTitle')}
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.75 }}>
                {t('options.typography.docDesc')}
              </Typography>
              <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1 }}>
                <FormControl sx={{ minWidth: 0 }}>
                  <Select
                    size="small"
                    value={draft?.docFontFamily ?? 'Inter'}
                    onChange={(e) => set('docFontFamily', e.target.value)}
                  >
                    {FONT_FAMILIES.map((f) => (
                      <MenuItem key={f} value={f}>
                        {f}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl sx={{ minWidth: 0 }}>
                  <Select
                    size="small"
                    value={draft?.docBaseFontSize ?? 11}
                    onChange={(e) => set('docBaseFontSize', Number(e.target.value))}
                  >
                    {sizeOptions.map((s) => (
                      <MenuItem key={s} value={s}>
                        {t('options.typography.docBaseFontSize')} : {s}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl sx={{ minWidth: 0 }}>
                  <Select
                    size="small"
                    value={draft?.docTableFontSize ?? 9}
                    onChange={(e) => set('docTableFontSize', Number(e.target.value))}
                  >
                    {sizeOptions.map((s) => (
                      <MenuItem key={s} value={s}>
                        {t('options.typography.docTableFontSize')} : {s}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl sx={{ minWidth: 0 }}>
                  <Select
                    size="small"
                    value={draft?.docHeaderFontSize ?? 13}
                    onChange={(e) => set('docHeaderFontSize', Number(e.target.value))}
                  >
                    {sizeOptions.map((s) => (
                      <MenuItem key={s} value={s}>
                        {t('options.typography.docHeaderFontSize')} : {s}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl sx={{ minWidth: 0 }}>
                  <Select
                    size="small"
                    value={draft?.docFooterFontSize ?? 9}
                    onChange={(e) => set('docFooterFontSize', Number(e.target.value))}
                  >
                    {sizeOptions.map((s) => (
                      <MenuItem key={s} value={s}>
                        {t('options.typography.docFooterFontSize')} : {s}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </Box>

              <Typography variant="caption" sx={{ fontWeight: 700, display: 'block', mt: 1.25 }}>
                {t('options.typography.previewTitle')}
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                {t('options.typography.previewDesc')}
              </Typography>
              <InvoicePreview
                fontFamily={draft?.docFontFamily ?? 'Inter'}
                baseFontSize={draft?.docBaseFontSize ?? 11}
                tableFontSize={draft?.docTableFontSize ?? 9}
                headerFontSize={draft?.docHeaderFontSize ?? 13}
                footerFontSize={draft?.docFooterFontSize ?? 9}
              />
            </SectionCard>
          </Vis>
        </Box>

        {/* RIGHT — Keyboard Center */}
        <Box sx={{ minHeight: 0, overflowY: 'auto', pl: 0.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
          <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.5 }}>
            {t('options.columns.shortcuts')}
          </Typography>
          <Vis show={keyboardMatch}>
            <Card variant="outlined" sx={{ borderColor: 'divider' }}>
              <CardContent sx={{ p: 1.75 }}>
                <KeyboardShortcuts
                  bindings={draftBindings}
                  onChange={setDraftBinding}
                  onReset={resetDraftBinding}
                  onResetAll={restoreDefaults}
                  onConflict={(id) =>
                    setSnack({
                      severity: 'error',
                      message: t('options.shortcuts.conflictAssigned', { command: t(labelById[id]) }),
                    })
                  }
                />
              </CardContent>
            </Card>
          </Vis>
        </Box>
      </Box>

      {/* Fixed bottom save bar */}
      <Box
        sx={{
          flexShrink: 0,
          position: { xs: 'static', md: 'sticky' },
          bottom: 0,
          mt: 1.25,
          pt: 1.25,
          borderTop: '1px solid',
          borderColor: 'divider',
          display: 'flex',
          justifyContent: 'flex-end',
          gap: 1.5,
          bgcolor: 'background.paper',
        }}
      >
        <Button variant="outlined" onClick={handleCancel} disabled={!dirty || saving}>
          {t('common.cancel')}
        </Button>
        <Button
          variant="contained"
          startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <Save size={16} />}
          disabled={!dirty || saving}
          onClick={() => void trySave()}
        >
          {t('options.save')}
        </Button>
      </Box>

      {/* Snackbar */}
      <Snackbar
        open={!!snack}
        autoHideDuration={4000}
        onClose={() => setSnack(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        {snack ? (
          <Alert severity={snack.severity} variant="filled" onClose={() => setSnack(null)}>
            {snack.message}
          </Alert>
        ) : undefined}
      </Snackbar>

      {/* Unsaved changes */}
      <Dialog open={unsavedOpen} onClose={onUnsavedCancel} maxWidth="xs" fullWidth>
        <DialogTitle>{t('options.unsaved.title')}</DialogTitle>
        <DialogContent>
          <DialogContentText>{t('options.unsaved.body')}</DialogContentText>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onUnsavedCancel}>{t('options.unsaved.cancel')}</Button>
          <Button onClick={onUnsavedDiscard} color="warning">
            {t('options.unsaved.discard')}
          </Button>
          <Button onClick={() => void onUnsavedSave()} variant="contained">
            {t('options.unsaved.save')}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Restore confirm */}
      <Dialog open={!!restoreTarget} onClose={restoring ? undefined : () => setRestoreTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>{t('options.dataBackup.restoreConfirmTitle')}</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {restoreTarget ? `${t('options.dataBackup.restoreConfirmBody')}\n\n${restoreTarget.fileName}` : ''}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRestoreTarget(null)} disabled={restoring}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="contained"
            color="error"
            onClick={() => void executeRestore()}
            disabled={restoring}
            startIcon={restoring ? <CircularProgress size={16} /> : <Undo2 size={16} />}
          >
            {restoring ? t('common.loading') : t('options.dataBackup.restore')}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Restore ready / restarting */}
      <Dialog open={Boolean(restoreMessage) || restarting} onClose={() => undefined} maxWidth="xs" fullWidth>
        <DialogContent sx={{ textAlign: 'center', py: 4 }}>
          <CircularProgress size={28} sx={{ mb: 2 }} />
          <Typography sx={{ fontWeight: 600 }}>{restoreMessage || t('options.dataBackup.restoreReady')}</Typography>
        </DialogContent>
      </Dialog>

      {/* Delete confirm */}
      <Dialog open={!!deleteTarget} onClose={deleting ? undefined : () => setDeleteTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>{t('common.delete')}</DialogTitle>
        <DialogContent>
          <DialogContentText>{deleteTarget ? t('options.dataBackup.deleteConfirm') : ''}</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteTarget(null)} disabled={deleting}>
            {t('common.cancel')}
          </Button>
          <Button variant="contained" color="error" onClick={() => void executeDelete()} disabled={deleting}>
            {deleting ? t('common.loading') : t('common.delete')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
