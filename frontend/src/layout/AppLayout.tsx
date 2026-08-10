import { useEffect, useState } from 'react';
import {
  Alert,
  AppBar,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Drawer,
  IconButton,
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
import { useUpdateStore } from '../stores/updateStore';
import { useSettingsStore } from '../stores/settingsStore';

const navItems = [
  { key: 'invoices', icon: FileText, path: '/invoices' },
  { key: 'clients', icon: Users, path: '/clients' },
  { key: 'products', icon: Package, path: '/products' },
  { key: 'reports', icon: WalletCards, path: '/reports' },
];

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
    checked,
    dialogOpen,
    dismissed,
    installing,
    installError,
    setUpdate,
    openDialog,
    dismissDialog,
    installNow,
  } = useUpdateStore();
  const [appVersion, setAppVersion] = useState('');

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

  useEffect(() => {
    const item = navItems.find((it) => location.pathname.startsWith(it.path));
    const page = item ? t(`nav.${item.key}`) : '';
    document.title = page ? `Mohasabi - ${page}` : 'Mohasabi';
  }, [location.pathname, i18n.language, t]);

  const openUpdateNotification = () => {
    setNotifAnchor(null);
    navigate('/options');
  };

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
            backgroundColor: '#FFFFFF',
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
                      border: '2px solid #fff',
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

      {/* Boîte de dialogue de mise à jour : purement informative, affichée après
          la vérification. « Plus tard » ne télécharge ni n'installe rien.
          « Mettre à jour » télécharge, vérifie l'empreinte SHA-256 côté API,
          puis installe et redémarre l'application. */}
      <Dialog open={dialogOpen} onClose={installing ? undefined : dismissDialog}>
        <DialogTitle sx={{ fontWeight: 700 }}>{t('update.dialogTitle')}</DialogTitle>
        <DialogContent sx={{ minWidth: 380 }}>
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
            {latestVersion && releaseNotes && (
              <Typography variant="body2" sx={{ color: 'text.secondary', mt: 1 }}>
                {releaseNotes}
              </Typography>
            )}
          </Box>
          {installError && (
            <Alert severity="error" sx={{ mb: 1.5, '& .MuiAlert-message': { fontSize: 13 } }}>
              {installError}
            </Alert>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button variant="outlined" onClick={dismissDialog} disabled={installing}>
            {t('update.plusTard')}
          </Button>
          <Button
            variant="contained"
            startIcon={installing ? <CircularProgress size={16} color="inherit" /> : undefined}
            disabled={installing}
            onClick={() => void installNow()}
          >
            {t('update.mettreAJour')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
