import { useEffect, useState, type MouseEvent } from 'react';
import {
  AppBar,
  Box,
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
  Globe,
  Menu as MenuIcon,
  Package,
  Settings,
  Users,
  WalletCards,
  X,
} from 'lucide-react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import { useUpdateStore } from '../stores/updateStore';

const navItems = [
  { key: 'invoices', icon: FileText, path: '/invoices' },
  { key: 'clients', icon: Users, path: '/clients' },
  { key: 'products', icon: Package, path: '/products' },
  { key: 'reports', icon: WalletCards, path: '/reports' },
  { key: 'settings', icon: Settings, path: '/settings' },
];

const languages = [
  { code: 'fr', label: 'Français' },
  { code: 'en', label: 'English' },
];

export default function AppLayout() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);

  const [langAnchor, setLangAnchor] = useState<HTMLElement | null>(null);
  const [notifAnchor, setNotifAnchor] = useState<HTMLElement | null>(null);

  const { updateAvailable, setUpdate } = useUpdateStore();
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
    navigate('/settings');
  };

  const changeLang = (code: string) => {
    localStorage.setItem('mohasabi_lang', code);
    void i18n.changeLanguage(code);
    document.documentElement.lang = code;
    setLangAnchor(null);
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
            {/* Language selector */}
            <Box
              onClick={(e: MouseEvent<HTMLDivElement>) => setLangAnchor(e.currentTarget)}
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: 0.75,
                px: 1.5,
                py: 1,
                borderRadius: 2,
                cursor: 'pointer',
                color: 'text.secondary',
                fontSize: 14,
                fontWeight: 600,
                transition: 'background-color 0.2s ease, color 0.2s ease',
                '&:hover': { backgroundColor: 'grey.100', color: 'text.primary' },
              }}
            >
              <Globe size={17} />
              <span>{languages.find((l) => l.code === i18n.language)?.label ?? 'Français'}</span>
            </Box>

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

      {/* Language menu */}
      <Menu
        anchorEl={langAnchor}
        open={Boolean(langAnchor)}
        onClose={() => setLangAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        {languages.map((l) => (
          <MenuItem
            key={l.code}
            onClick={() => changeLang(l.code)}
            selected={i18n.language === l.code}
            sx={{ minWidth: 160, fontWeight: i18n.language === l.code ? 700 : 500 }}
          >
            {l.label}
          </MenuItem>
        ))}
      </Menu>

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
    </Box>
  );
}
