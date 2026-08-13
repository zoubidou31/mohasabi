import { useEffect, useRef, useState } from 'react';
import {
  Alert,
  AppBar,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Drawer,
  FormControlLabel,
  IconButton,
  LinearProgress,
  List,
  Menu,
  MenuItem,
  Toolbar,
  Tooltip,
  Typography,
} from '@mui/material';
import {
  Bell,
  FileText,
  Menu as MenuIcon,
  Package,
  SlidersHorizontal,
  Users,
  WalletCards,
  X,
} from 'lucide-react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import { useUpdateStore, type UpdateInstallStatus } from '../stores/updateStore';
import { useSettingsStore } from '../stores/settingsStore';
import { COMMAND_IDS, useGlobalCommand, useGlobalShortcuts } from '../utils/shortcuts';
import CommandPalette from '../components/CommandPalette';

const navItems = [
  { key: 'invoices', icon: FileText, path: '/invoices' },
  { key: 'clients', icon: Users, path: '/clients' },
  { key: 'products', icon: Package, path: '/products' },
  { key: 'reports', icon: WalletCards, path: '/reports' },
];

// Répare le double encodage (« Ã© » → « é ») si la source du manifest a été
// lue comme Latin-1 alors qu'elle était UTF-8. Ne modifie rien sinon.
function fixEncoding(text: string): string {
  if (!/[\u00C0-\u00FF]/.test(text)) return text;
  try {
    const bytes = new Uint8Array(text.length);
    for (let i = 0; i < text.length; i++) {
      const code = text.charCodeAt(i);
      if (code > 0xff) return text;
      bytes[i] = code;
    }
    const decoded = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    return decoded.includes('\uFFFD') ? text : decoded;
  } catch {
    return text;
  }
}

// Extrait 3 à 6 puces lisibles depuis les notes de version brutes (markdown/plain).
function parseReleaseNotes(raw?: string): string[] {
  if (!raw) return [];
  return fixEncoding(raw)
    .split(/\r?\n/)
    .map((line) =>
      line
        .trim()
        .replace(/^[-*+•]\s+/, '')
        .replace(/^#+\s*/, '')
        .replace(/\*\*/g, '')
        .trim(),
    )
    .filter((line) => line.length > 0)
    .slice(0, 6);
}

function formatBytes(bytes?: number | null): string {
  if (!bytes || bytes <= 0) return '';
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} Mo`;
  return `${Math.max(1, Math.round(bytes / 1024))} Ko`;
}

function formatEta(seconds: number | null): string {
  if (!seconds || seconds <= 0) return '…';
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return m > 0 ? `${m} min ${s} s` : `${s} s`;
}

export default function AppLayout() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);

  const [notifAnchor, setNotifAnchor] = useState<HTMLElement | null>(null);
  const [uncleanDismissed, setUncleanDismissed] = useState(false);
  const uncleanExit = useSettingsStore((s) => s.uncleanExit);

  const {
    updateAvailable,
    latestVersion,
    currentVersion,
    releaseNotes,
    sizeBytes,
    checked,
    dialogOpen,
    dismissed,
    installing,
    installError,
    installStatus,
    setUpdate,
    openDialog,
    dismissDialog,
    setInstallStatus,
    installNow,
  } = useUpdateStore();
  const [appVersion, setAppVersion] = useState('');
  const [launchAfterUpdate, setLaunchAfterUpdate] = useState(true);
  const [etaSeconds, setEtaSeconds] = useState<number | null>(null);
  const installStartedAt = useRef(0);
  const notes = parseReleaseNotes(releaseNotes);

  useEffect(() => {
    let cancelled = false;
    api
      .get<{
        success: boolean;
        updateAvailable: boolean;
        currentVersion?: string;
        latestVersion?: string;
        releaseNotes?: string;
      }>('/update/check')
      .then(({ data }) => {
        if (cancelled) return;
        if (data.success) {
          setUpdate({
            updateAvailable: data.updateAvailable,
            latestVersion: data.latestVersion,
            releaseNotes: data.releaseNotes,
            currentVersion: data.currentVersion,
          });
        }
      })
      .catch(() => {
        // Serveur indisponible : aucune notification.
      });
    return () => {
      cancelled = true;
    };
  }, [setUpdate]);

  // Détection d'une mise à jour : la boîte de dialogue d'information s'affiche
  // une seule fois par session, uniquement après la vérification (jamais de
  // téléchargement ni d'installation automatique — « Plus tard » ne fait rien).
  useEffect(() => {
    if (updateAvailable && checked && !dismissed && !dialogOpen) {
      openDialog();
    }
  }, [updateAvailable, checked, dismissed, dialogOpen, openDialog]);

  // La version affichée dans le footer provient toujours de l'API locale
  // (/api/version, renvoyée depuis l'assemblage) — jamais d'une constante codée en dur.
  useEffect(() => {
    let cancelled = false;
    api
      .get<{ version: string }>('/version')
      .then(({ data }) => {
        if (!cancelled && data?.version) setAppVersion(data.version);
      })
      .catch(() => {
        // Serveur indisponible : le footer indiquera simplement "Mohasabi".
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Pendant le téléchargement, interroge l'état de l'installation pour afficher
  // la progression (pourcentage, octets, temps restant estimé, statut).
  useEffect(() => {
    if (!installing) return;
    let cancelled = false;
    const started = installStartedAt.current;
    const tick = async () => {
      if (cancelled) return;
      try {
        const { data } = await api.get<UpdateInstallStatus>('/update/install/status');
        if (cancelled) return;
        setInstallStatus(data);
        if (data.totalBytes && data.downloadedBytes > 0 && started > 0) {
          const elapsed = (Date.now() - started) / 1000;
          if (elapsed > 0) {
            const speed = data.downloadedBytes / elapsed;
            const remaining = (data.totalBytes - data.downloadedBytes) / speed;
            setEtaSeconds(remaining > 0 ? Math.ceil(remaining) : 0);
          }
        }
      } catch {
        // Serveur momentanément indisponible : ignoré.
      }
    };
    const id = window.setInterval(() => void tick(), 600);
    return () => {
      cancelled = true;
      window.clearInterval(id);
    };
  }, [installing, setInstallStatus]);

  const handleInstall = () => {
    installStartedAt.current = Date.now();
    setEtaSeconds(null);
    void installNow(launchAfterUpdate);
  };

  useEffect(() => {
    const item = navItems.find((it) => location.pathname.startsWith(it.path));
    const page = item ? t(`nav.${item.key}`) : '';
    document.title = page ? `Mohasabi - ${page}` : 'Mohasabi';
  }, [location.pathname, i18n.language, t]);

  const openUpdateNotification = () => {
    setNotifAnchor(null);
    navigate('/options');
  };

  // Raccourcis clavier globaux : la saisie est écoutée une fois ici et
  // déclenche les handlers enregistrés par la page active (useCommand).
  useGlobalShortcuts();

  const [paletteOpen, setPaletteOpen] = useState(false);

  // Commandes GLOBALES (actives sur toutes les pages). Chaque page enregistre ses
  // propres handlers via useCommand et prime sur ceux-ci pour une même commande.
  useGlobalCommand(COMMAND_IDS.NEW, () => {
    navigate('/invoices/new');
    setMobileOpen(false);
  });
  useGlobalCommand(COMMAND_IDS.GLOBAL_SEARCH, () => setPaletteOpen((o) => !o));
  useGlobalCommand(COMMAND_IDS.HELP, () => setPaletteOpen(true));
  useGlobalCommand(COMMAND_IDS.NAV_DASHBOARD, () => navigate('/invoices'));
  useGlobalCommand(COMMAND_IDS.NAV_INVOICES, () => navigate('/invoices'));
  useGlobalCommand(COMMAND_IDS.NAV_CLIENTS, () => navigate('/clients'));
  useGlobalCommand(COMMAND_IDS.NAV_PRODUCTS, () => navigate('/products'));
  useGlobalCommand(COMMAND_IDS.NAV_REPORTS, () => navigate('/reports'));
  useGlobalCommand(COMMAND_IDS.NAV_SETTINGS, () => navigate('/options'));
  useGlobalCommand(COMMAND_IDS.NAV_BACK, () => navigate(-1));
  useGlobalCommand(COMMAND_IDS.NAV_FORWARD, () => navigate(1));

  const navContent = (isMobile: boolean) => (
    <Box
      component="nav"
      sx={{
        display: 'flex',
        flexDirection: isMobile ? 'column' : 'row',
        alignItems: isMobile ? 'stretch' : 'center',
        gap: 0.5,
        flexGrow: 1,
        justifyContent: 'center',
      }}
    >
      {navItems.map((item) => {
        const Icon = item.icon;
        const active = location.pathname.startsWith(item.path);
        return (
          <Box
            key={item.key}
            onClick={() => {
              navigate(item.path);
              setMobileOpen(false);
            }}
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              cursor: 'pointer',
              userSelect: 'none',
              px: 2,
              py: 1.25,
              borderRadius: 2,
              color: active ? 'primary.main' : 'text.secondary',
              fontWeight: active ? 700 : 600,
              fontSize: 14,
              position: 'relative',
              transition: 'color 0.2s ease, background-color 0.2s ease',
              '&:hover': {
                color: 'primary.main',
                backgroundColor: 'primary.light',
              },
              '&::after': {
                content: active ? '""' : 'none',
                position: 'absolute',
                bottom: 0,
                left: '16px',
                right: '16px',
                height: 3,
                borderRadius: '3px 3px 0 0',
                backgroundColor: 'primary.main',
              },
            }}
          >
            <Icon size={17} strokeWidth={active ? 2.4 : 2} />
            <span>{t(`nav.${item.key}`)}</span>
          </Box>
        );
      })}
    </Box>
  );

  const percent = installStatus?.percent ?? null;
  const installPhaseLabel =
    installStatus?.phase === 'downloading'
      ? t('update.downloading', { percent: percent ?? 0 })
      : installStatus?.phase === 'verifying'
        ? t('update.verifying')
        : installStatus?.phase === 'launching'
          ? t('update.launching')
          : '';

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      <AppBar position="fixed" elevation={0} sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
        <Toolbar
          sx={{
            minHeight: 72,
            px: { xs: 2, md: 3 },
            gap: { xs: 1.5, md: 3 },
            borderBottom: '1px solid',
            borderColor: 'divider',
            backgroundColor: 'background.paper',
          }}
        >
          {/* Desktop nav */}
          <Box sx={{ display: { xs: 'none', md: 'block' }, flexGrow: 1 }}>{navContent(false)}</Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, flexShrink: 0 }}>
            {/* Notifications */}
            <Tooltip title={t('common.notifications')}>
              <IconButton onClick={(e) => setNotifAnchor(e.currentTarget)} sx={{ position: 'relative' }}>
                <Bell size={19} />
                {updateAvailable && (
                  <Box
                    sx={{
                      position: 'absolute',
                      top: 8,
                      right: 9,
                      minWidth: 16,
                      height: 16,
                      px: 0.5,
                      borderRadius: 8,
                      display: 'grid',
                      placeItems: 'center',
                      backgroundColor: 'error.main',
                      border: '2px solid',
                      borderColor: 'background.paper',
                      fontSize: 9,
                      fontWeight: 800,
                      color: '#fff',
                      lineHeight: 1,
                    }}
                  >
                    1
                  </Box>
                )}
              </IconButton>
            </Tooltip>

            {/* Options */}
            <Tooltip title={t('nav.options')}>
              <IconButton onClick={() => navigate('/options')}>
                <SlidersHorizontal size={19} />
              </IconButton>
            </Tooltip>

            {/* Mobile menu button */}
            <IconButton
              sx={{ display: { md: 'none' }, ml: 0.5 }}
              onClick={() => setMobileOpen(true)}
              aria-label="menu"
            >
              <MenuIcon size={20} />
            </IconButton>
          </Box>
        </Toolbar>
      </AppBar>

      {/* Notifications menu */}
      <Menu
        anchorEl={notifAnchor}
        open={Boolean(notifAnchor)}
        onClose={() => setNotifAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        <Box sx={{ px: 2, py: 1.5, minWidth: 300 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 14 }}>{t('common.notifications')}</Typography>
          {updateAvailable ? (
            <MenuItem
              onClick={openUpdateNotification}
              sx={{ px: 0, mt: 1, whiteSpace: 'normal', alignItems: 'flex-start', borderRadius: 2 }}
            >
              <Box>
                <Typography sx={{ fontSize: 13, fontWeight: 600, color: 'primary.main' }}>
                  {t('update.notification')}
                </Typography>
              </Box>
            </MenuItem>
          ) : (
            <Typography sx={{ color: 'text.secondary', fontSize: 13, mt: 0.5 }}>
              {t('common.noNotifications')}
            </Typography>
          )}
        </Box>
      </Menu>

      {/* Mobile drawer */}
      <Drawer
        anchor="left"
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        ModalProps={{ keepMounted: true }}
        sx={{ display: { xs: 'block', md: 'none' }, '& .MuiDrawer-paper': { width: 280 } }}
      >
        <Toolbar sx={{ minHeight: 72, gap: 1.5, px: 2 }}>
          <Box
            component="img"
            src="/mohasabi.png"
            alt={t('appName')}
            sx={{
              width: 36,
              height: 36,
              objectFit: 'contain',
              borderRadius: 1,
            }}
          />
          <Box sx={{ flexGrow: 1 }}>
            <Typography sx={{ fontWeight: 800, fontSize: 16 }}>{t('appName')}</Typography>
            <Typography sx={{ fontSize: 11.5, color: 'text.secondary' }}>{t('appSubtitle')}</Typography>
          </Box>
          <IconButton onClick={() => setMobileOpen(false)}>
            <X size={19} />
          </IconButton>
        </Toolbar>
        <List sx={{ px: 1.5, pt: 1 }}>{navContent(true)}</List>
      </Drawer>

      {/* Main content */}
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          width: '100%',
          pt: '96px',
          pb: 7,
          px: { xs: 2, sm: 3, md: 4 },
          maxWidth: '1600px',
          mx: 'auto',
        }}
      >
        {uncleanExit && !uncleanDismissed && (
          <Alert
            severity="info"
            variant="outlined"
            onClose={() => setUncleanDismissed(true)}
            sx={{ mb: 2, alignItems: 'center' }}
          >
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.25 }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13.5 }}>{t('uncleanExit.title')}</Typography>
              <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('uncleanExit.body')}</Typography>
            </Box>
          </Alert>
        )}
        <Outlet />
      </Box>

      <CommandPalette open={paletteOpen} onClose={() => setPaletteOpen(false)} />

      {/* Version footer — fixed at the bottom of the viewport, visible on every
          page without overlapping page content (main padding clears its height). */}
      <Box
        component="footer"
        sx={{
          position: 'fixed',
          bottom: 0,
          left: 0,
          width: '100%',
          py: 1.5,
          px: 2,
          textAlign: 'center',
          borderTop: '1px solid',
          borderColor: 'divider',
          backgroundColor: 'background.paper',
          color: 'text.secondary',
          fontSize: 12,
          zIndex: (theme) => theme.zIndex.appBar - 1,
        }}
      >
        {appVersion ? `Mohasabi v${appVersion}` : 'Mohasabi'}
      </Box>

      {/* Boîte de dialogue de mise à jour : version actuelle / nouvelle version,
          taille, temps estimé, nouveautés (puces), progression du téléchargement
          et choix de relance. « Plus tard » ne télécharge ni n'installe rien.
          « Mettre à jour » télécharge, vérifie l'empreinte SHA-256 côté API,
          puis installe (avec relance automatique selon la case cochée). */}
      <Dialog open={dialogOpen} onClose={installing ? undefined : dismissDialog}>
        <DialogTitle sx={{ fontWeight: 700 }}>{t('update.dialogTitle')}</DialogTitle>
        <DialogContent sx={{ minWidth: 400 }}>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, mb: 1 }}>
            <Typography variant="body2">
              <Box component="span" sx={{ fontWeight: 600 }}>
                {t('update.currentVersion')} :{' '}
              </Box>
              {currentVersion ?? appVersion}
            </Typography>
            <Typography variant="body2">
              <Box component="span" sx={{ fontWeight: 600 }}>
                {t('update.newVersion')} :{' '}
              </Box>
              {latestVersion}
            </Typography>
            <Typography variant="body2">
              <Box component="span" sx={{ fontWeight: 600 }}>
                {t('update.size')} :{' '}
              </Box>
              {formatBytes(sizeBytes) || (installStatus?.totalBytes ? formatBytes(installStatus.totalBytes) : '—')}
            </Typography>
            {notes.length > 0 && (
              <Box sx={{ mt: 1 }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
                  {t('update.whatNew')}
                </Typography>
                {notes.map((note, idx) => (
                  <Typography key={idx} component="li" variant="body2" sx={{ color: 'text.secondary', ml: 2, fontSize: 13 }}>
                    {note}
                  </Typography>
                ))}
              </Box>
            )}
          </Box>

          {installing && (
            <Box sx={{ mt: 1.5, mb: 0.5 }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.75 }}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {installPhaseLabel || (installStatus?.message ?? '')}
                </Typography>
                {etaSeconds !== null && (
                  <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                    {t('update.estimatedTime')} : {formatEta(etaSeconds)}
                  </Typography>
                )}
              </Box>
              {percent !== null ? (
                <LinearProgress variant="determinate" value={percent} sx={{ height: 8, borderRadius: 4 }} />
              ) : (
                <LinearProgress sx={{ height: 8, borderRadius: 4 }} />
              )}
              {installStatus && (installStatus.downloadedBytes > 0 || installStatus.totalBytes) && (
                <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', mt: 0.5 }}>
                  {t('update.progressStatus', {
                    downloaded: formatBytes(installStatus.downloadedBytes),
                    total: formatBytes(installStatus.totalBytes),
                  })}
                </Typography>
              )}
            </Box>
          )}

          {installError && (
            <Alert severity="error" sx={{ mb: 1.5, '& .MuiAlert-message': { fontSize: 13 } }}>
              {installError}
            </Alert>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2, flexDirection: 'column', alignItems: 'stretch', gap: 0.5 }}>
          <FormControlLabel
            control={<Checkbox checked={launchAfterUpdate} onChange={(e) => setLaunchAfterUpdate(e.target.checked)} disabled={installing} />}
            label={<Typography variant="body2">{t('update.launchAfterUpdate')}</Typography>}
            sx={{ ml: 0 }}
          />
          <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1.5 }}>
            <Button variant="outlined" onClick={dismissDialog} disabled={installing}>
              {t('update.plusTard')}
            </Button>
            <Button
              variant="contained"
              startIcon={installing ? <CircularProgress size={16} color="inherit" /> : undefined}
              disabled={installing}
              onClick={handleInstall}
            >
              {t('update.mettreAJour')}
            </Button>
          </Box>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
