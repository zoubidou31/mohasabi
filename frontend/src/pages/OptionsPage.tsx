import { useCallback, useEffect, useMemo, useState } from 'react';
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
  InputLabel,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  MenuItem,
  Select,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
  useTheme,
} from '@mui/material';
import {
  Building2,
  Database,
  FolderOpen,
  HardDrive,
  Keyboard,
  MonitorCog,
  Palette,
  Save,
  ShieldCheck,
  Trash2,
  Undo2,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { AppSettings, BackupInfo, BackupRunResult, BackupStatus, RestoreResult } from '../api/types';
import { formatDateTime, formatNumber } from '../utils/format';
import PageHeader from '../components/PageHeader';
import { useSettingsStore } from '../stores/settingsStore';
import { SHORTCUT_EVENTS, useShortcutEvent } from '../utils/shortcuts';

const frequencyOptions = [5, 15, 30, 60, 360, 1440];
const retentionOptions = [0, 3, 5, 10];

export default function OptionsPage() {
  const { t } = useTranslation();
  const theme = useTheme();
  const navigate = useNavigate();

  const settings = useSettingsStore((s) => s.settings);
  const saveSettings = useSettingsStore((s) => s.save);
  const restartApp = useSettingsStore((s) => s.restartApp);

  const [draft, setDraft] = useState<AppSettings | null>(settings);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<{ severity: 'success' | 'error'; message: string } | null>(null);

  const [status, setStatus] = useState<BackupStatus | null>(null);
  const [backups, setBackups] = useState<BackupInfo[]>([]);
  const [backingUp, setBackingUp] = useState(false);

  const [restoreTarget, setRestoreTarget] = useState<BackupInfo | null>(null);
  const [restoring, setRestoring] = useState(false);
  const [restoreMessage, setRestoreMessage] = useState('');
  const [restarting, setRestarting] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<BackupInfo | null>(null);
  const [deleting, setDeleting] = useState(false);

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

  const dirty = useMemo(() => {
    if (!settings || !draft) return false;
    return JSON.stringify(settings) !== JSON.stringify(draft);
  }, [settings, draft]);

  const set = <K extends keyof AppSettings>(key: K, value: AppSettings[K]) => {
    setDraft((d) => (d ? { ...d, [key]: value } : d));
    setNotice(null);
  };

  const handleSave = async () => {
    if (!draft) return;
    setSaving(true);
    setNotice(null);
    try {
      await saveSettings(draft);
      setNotice({ severity: 'success', message: t('options.saveSuccess') });
    } catch (err) {
      setNotice({ severity: 'error', message: t('options.saveError') + ' : ' + extractError(err) });
    } finally {
      setSaving(false);
    }
  };

  useShortcutEvent(SHORTCUT_EVENTS.SAVE, () => {
    if (dirty) void handleSave();
  });

  const handleBackupNow = async () => {
    setBackingUp(true);
    setNotice(null);
    try {
      const { data } = await api.post<BackupRunResult>('/backup/now');
      if (data.success) {
        setNotice({ severity: 'success', message: t('options.dataBackup.lastBackupOk') });
      } else {
        setNotice({ severity: 'error', message: data.error ?? t('options.dataBackup.lastBackupFailed') });
      }
    } catch (err) {
      setNotice({ severity: 'error', message: extractError(err) });
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
    setNotice(null);
    try {
      const { data } = await api.post<RestoreResult>('/restore', { fileName: restoreTarget.fileName });
      if (data.success) {
        setRestoreTarget(null);
        setRestoreMessage(data.message ?? t('options.dataBackup.restoreReady'));
        if (data.requiresRestart) {
          void restartApp().then(() => setRestarting(true));
        }
      } else {
        setNotice({ severity: 'error', message: data.error ?? t('options.dataBackup.restoreError') });
        setRestoreTarget(null);
      }
    } catch (err) {
      setNotice({ severity: 'error', message: extractError(err) });
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
      setNotice({ severity: 'error', message: extractError(err) });
    } finally {
      setDeleting(false);
      setDeleteTarget(null);
      void refresh();
    }
  };

  const lastBackupText = status?.lastBackupAt ? formatDateTime(status.lastBackupAt) : t('options.dataBackup.lastBackupNever');
  const lastBackupOk = status?.lastBackupStatus !== 'failed';

  return (
    <Box>
      <PageHeader title={t('options.title')} description={t('options.description')} />

      {notice && (
        <Alert severity={notice.severity} sx={{ mb: 2 }} onClose={() => setNotice(null)}>
          {notice.message}
        </Alert>
      )}

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
        {/* Général */}
        <Card>
          <CardContent sx={{ p: 3 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 0.5 }}>
              <Palette size={18} color={theme.palette.primary.main} />
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                {t('options.general.title')}
              </Typography>
            </Box>
            <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2.5 }}>
              {t('options.general.description')}
            </Typography>
            <Box sx={{ display: 'flex', gap: 2.5, flexWrap: 'wrap' }}>
              <FormControl sx={{ minWidth: 220 }}>
                <InputLabel>{t('options.general.language')}</InputLabel>
                <Select label={t('options.general.language')} value={draft?.language ?? 'fr'} onChange={(e) => set('language', e.target.value)}>
                  <MenuItem value="fr">Français</MenuItem>
                  <MenuItem value="en">English</MenuItem>
                </Select>
              </FormControl>
              <FormControl sx={{ minWidth: 220 }}>
                <InputLabel>{t('options.general.theme')}</InputLabel>
                <Select label={t('options.general.theme')} value={draft?.theme ?? 'light'} onChange={(e) => set('theme', e.target.value)}>
                  <MenuItem value="light">{t('options.general.themeLight')}</MenuItem>
                  <MenuItem value="dark">{t('options.general.themeDark')}</MenuItem>
                  <MenuItem value="system">{t('options.general.themeSystem')}</MenuItem>
                </Select>
              </FormControl>
            </Box>
          </CardContent>
        </Card>

        {/* Données & Sauvegarde */}
        <Card>
          <CardContent sx={{ p: 3 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 0.5 }}>
              <ShieldCheck size={18} style={{ color: 'success.main' }} />
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                {t('options.dataBackup.title')}
              </Typography>
            </Box>
            <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2.5 }}>
              {t('options.dataBackup.description')}
            </Typography>

            <FormControlLabel
              control={<Switch checked={draft?.autoBackupEnabled ?? true} onChange={(e) => set('autoBackupEnabled', e.target.checked)} />}
              label={t('options.dataBackup.autoBackup')}
            />
            <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', mb: 2 }}>
              {t('options.dataBackup.autoBackupHint')}
            </Typography>

            <Box sx={{ display: 'flex', gap: 2.5, flexWrap: 'wrap' }}>
              <FormControl sx={{ minWidth: 240 }} disabled={!(draft?.autoBackupEnabled ?? true)}>
                <InputLabel>{t('options.dataBackup.frequency')}</InputLabel>
                <Select
                  label={t('options.dataBackup.frequency')}
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
              <FormControl sx={{ minWidth: 220 }}>
                <InputLabel>{t('options.dataBackup.retention')}</InputLabel>
                <Select label={t('options.dataBackup.retention')} value={draft?.backupRetentionCount ?? 5} onChange={(e) => set('backupRetentionCount', Number(e.target.value))}>
                  {retentionOptions.map((r) => (
                    <MenuItem key={r} value={r}>
                      {t(`options.dataBackup.retention${r}`)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Box>

            <Divider sx={{ my: 2.5 }} />

            <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
                <HardDrive size={18} style={{ color: 'text.secondary' }} />
                <Box>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {t('options.dataBackup.backupLocation')}
                  </Typography>
                  <Typography variant="caption" sx={{ color: 'text.secondary', wordBreak: 'break-all' }}>
                    {draft?.backupLocation || '—'}
                  </Typography>
                </Box>
              </Box>
              <Box sx={{ display: 'flex', gap: 1 }}>
                <Button variant="outlined" startIcon={<FolderOpen size={16} />} onClick={() => void handleOpenFolder()}>
                  {t('options.dataBackup.openFolder')}
                </Button>
                <Button variant="contained" startIcon={backingUp ? <CircularProgress size={16} color="inherit" /> : <Database size={16} />} disabled={backingUp} onClick={() => void handleBackupNow()}>
                  {backingUp ? t('options.dataBackup.backupRunning') : t('options.dataBackup.backupNow')}
                </Button>
              </Box>
            </Box>

            <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', mt: 2, alignItems: 'center' }}>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {t('options.dataBackup.lastBackup')} :
              </Typography>
              <Chip
                size="small"
                color={lastBackupOk ? 'success' : 'error'}
                variant="outlined"
                label={`${lastBackupText} — ${lastBackupOk ? t('options.dataBackup.lastBackupOk') : t('options.dataBackup.lastBackupFailed')}`}
              />
              <Chip size="small" variant="outlined" label={t('options.dataBackup.backupsCount', { count: status?.backupCount ?? 0 })} />
              <Chip size="small" variant="outlined" label={`${t('options.dataBackup.totalSize')} : ${formatNumber((status?.totalSize ?? 0) / 1024 / 1024)} Mo`} />
            </Box>

            <Typography variant="subtitle2" sx={{ fontWeight: 700, mt: 3, mb: 1 }}>
              {t('options.dataBackup.listTitle')}
            </Typography>
            <TableContainer component={Box} sx={{ boxShadow: 'none', border: '1px solid', borderColor: 'divider' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Fichier</TableCell>
                    <TableCell>{t('common.date')}</TableCell>
                    <TableCell align="right">{t('options.dataBackup.totalSize')}</TableCell>
                    <TableCell align="right">{t('common.actions')}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {backups.map((b) => (
                    <TableRow key={b.fileName}>
                      <TableCell sx={{ fontWeight: 600 }}>{b.fileName}</TableCell>
                      <TableCell>{formatDateTime(b.createdAt)}</TableCell>
                      <TableCell align="right" className="tnum">{formatNumber(b.size / 1024 / 1024)} Mo</TableCell>
                      <TableCell align="right">
                        <Box sx={{ display: 'inline-flex', gap: 0.5 }}>
                          <Button size="small" variant="outlined" startIcon={<Undo2 size={14} />} onClick={() => confirmRestore(b)}>
                            {t('options.dataBackup.restore')}
                          </Button>
                          <Button size="small" color="error" startIcon={<Trash2 size={14} />} onClick={() => confirmDelete(b)}>
                            {t('options.dataBackup.delete')}
                          </Button>
                        </Box>
                      </TableCell>
                    </TableRow>
                  ))}
                  {backups.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={4} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                        {t('options.dataBackup.empty')}
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </CardContent>
        </Card>

        {/* Affichage & Expérience */}
        <Card>
          <CardContent sx={{ p: 3 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 0.5 }}>
              <MonitorCog size={18} style={{ color: 'info.main' }} />
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                {t('options.display.title')}
              </Typography>
            </Box>
            <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
              {t('options.display.description')}
            </Typography>
            <FormControlLabel
              control={<Switch checked={draft?.splashEnabled ?? true} onChange={(e) => set('splashEnabled', e.target.checked)} />}
              label={t('options.display.splash')}
            />
            <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block' }}>
              {t('options.display.splashHint')}
            </Typography>
          </CardContent>
        </Card>

        {/* Société */}
        <Card>
          <CardContent sx={{ p: 3 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 0.5 }}>
              <Building2 size={18} color={theme.palette.primary.main} />
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                {t('options.company.title')}
              </Typography>
            </Box>
            <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2 }}>
              {t('options.company.description')}
            </Typography>
            <Button variant="outlined" onClick={() => navigate('/settings')}>
              {t('options.company.open')}
            </Button>
          </CardContent>
        </Card>

        {/* Raccourcis */}
        <Card>
          <CardContent sx={{ p: 3 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 1 }}>
              <Keyboard size={18} style={{ color: 'text.secondary' }} />
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                {t('options.shortcuts.title')}
              </Typography>
            </Box>
            <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1 }}>
              {t('options.shortcuts.description')}
            </Typography>
            <List dense disablePadding>
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 34 }}>
                  <Chip size="small" label="Ctrl + N" />
                </ListItemIcon>
                <ListItemText primary={t('options.shortcuts.newInvoice')} />
              </ListItem>
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 34 }}>
                  <Chip size="small" label="Ctrl + J" />
                </ListItemIcon>
                <ListItemText primary={t('options.shortcuts.newInvoice')} />
              </ListItem>
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 34 }}>
                  <Chip size="small" label="Ctrl + S" />
                </ListItemIcon>
                <ListItemText primary={t('options.shortcuts.save')} />
              </ListItem>
              <ListItem disableGutters>
                <ListItemIcon sx={{ minWidth: 34 }}>
                  <Chip size="small" label="Ctrl + F" />
                </ListItemIcon>
                <ListItemText primary={t('options.shortcuts.search')} />
              </ListItem>
            </List>
          </CardContent>
        </Card>

        {/* Enregistrer */}
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1.5, pt: 1 }}>
          <Button variant="outlined" onClick={() => setDraft(settings)} disabled={!dirty || saving}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="contained"
            startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <Save size={16} />}
            disabled={!dirty || saving}
            onClick={() => void handleSave()}
          >
            {t('options.save')}
          </Button>
        </Box>
      </Box>

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
          <Button variant="contained" color="error" onClick={() => void executeRestore()} disabled={restoring} startIcon={restoring ? <CircularProgress size={16} /> : <Undo2 size={16} />}>
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
