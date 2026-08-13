import { createTheme, type Theme, type ThemeOptions } from '@mui/material/styles';

type Mode = 'light' | 'dark';

const lightColors = {
  primary: '#157347',
  primaryDark: '#0F5A37',
  primaryLight: '#E8F5EC',
  primarySoft: '#F3F9F6',
  background: '#F7F9FB',
  card: '#FFFFFF',
  border: '#E9ECEF',
  text: '#1F2937',
  textSecondary: '#6B7280',
  success: '#157347',
  successBg: '#E6F4EC',
  info: '#1A56DB',
  infoBg: '#E8F0FE',
  warning: '#B45309',
  warningBg: '#FEF3C7',
  error: '#C81E1E',
  errorBg: '#FDECEC',
  gray: '#6B7280',
  grayBg: '#F3F4F6',
  hover: '#F6FAF8',
  tableRowHover: '#F6FAF8',
  inputBg: '#FFFFFF',
  menuHover: '#F8FAFB',
  appBarBg: '#FFFFFF',
  borderStrong: '#C9D2DA',
};

const darkColors = {
  primary: '#3FBF86',
  primaryDark: '#57D09A',
  primaryLight: '#123D2B',
  primarySoft: '#14251D',
  background: '#0F1419',
  card: '#1A2029',
  border: '#2A323D',
  text: '#F3F4F6',
  textSecondary: '#9CA3AF',
  success: '#3FBF86',
  successBg: '#123B2B',
  info: '#7AA2FF',
  infoBg: '#1B2A4A',
  warning: '#E8A33D',
  warningBg: '#3A2E14',
  error: '#F4716B',
  errorBg: '#3A1D1D',
  gray: '#9CA3AF',
  grayBg: '#232B35',
  hover: '#222B36',
  tableRowHover: '#1F2830',
  inputBg: '#141A22',
  menuHover: '#232B35',
  appBarBg: '#121821',
  borderStrong: '#3A4450',
};

const softShadow =
  '0 1px 2px rgba(16, 24, 40, 0.04), 0 1px 3px rgba(16, 24, 40, 0.06)';

export function createAppTheme(mode: Mode): Theme {
  const c = mode === 'dark' ? darkColors : lightColors;
  const themeOptions: ThemeOptions = {
    direction: 'ltr',
    palette: {
      mode,
      primary: { main: c.primary, dark: c.primaryDark, light: c.primaryLight, contrastText: mode === 'dark' ? '#0B0F14' : '#FFFFFF' },
      secondary: { main: mode === 'dark' ? '#818CF8' : '#4F46E5' },
      success: { main: c.success },
      info: { main: c.info },
      warning: { main: mode === 'dark' ? '#E8A33D' : '#D97706' },
      error: { main: c.error },
      background: { default: c.background, paper: c.card },
      text: { primary: c.text, secondary: c.textSecondary },
      divider: c.border,
    },
    shape: { borderRadius: 16 },
    typography: {
      fontFamily:
        "var(--moha-app-font, 'Inter'), -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif",
      h4: { fontWeight: 800, letterSpacing: '-0.02em' },
      h5: { fontWeight: 800, letterSpacing: '-0.01em' },
      h6: { fontWeight: 700 },
      button: { textTransform: 'none', fontWeight: 600, letterSpacing: '0.01em' },
    },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          body: {
            backgroundColor: c.background,
            color: c.text,
            WebkitFontSmoothing: 'antialiased',
            MozOsxFontSmoothing: 'grayscale',
          },
        },
      },
      MuiButton: {
        styleOverrides: {
          root: {
            borderRadius: 10,
            boxShadow: 'none',
            transition: 'all 0.2s cubic-bezier(0.4, 0, 0.2, 1)',
          },
          sizeLarge: { minHeight: 46, paddingLeft: 24, paddingRight: 24, borderRadius: 12 },
          sizeMedium: { minHeight: 40, paddingLeft: 18, paddingRight: 18 },
          containedPrimary: {
            background: c.primary,
            boxShadow: mode === 'dark' ? '0 4px 12px rgba(63, 191, 134, 0.18)' : '0 4px 12px rgba(21, 115, 71, 0.25)',
            '&:hover': {
              background: c.primaryDark,
              boxShadow: mode === 'dark' ? '0 6px 16px rgba(63, 191, 134, 0.24)' : '0 6px 16px rgba(21, 115, 71, 0.32)',
              transform: 'translateY(-1px)',
            },
          },
          outlined: {
            borderColor: c.border,
            color: c.text,
            '&:hover': { borderColor: c.borderStrong, background: c.menuHover },
          },
          outlinedPrimary: { borderColor: c.primary, color: c.primary },
          containedError: {
            background: c.error,
            boxShadow: mode === 'dark' ? '0 4px 12px rgba(244, 113, 107, 0.18)' : '0 4px 12px rgba(200, 30, 30, 0.22)',
            '&:hover': { background: mode === 'dark' ? '#FF8A84' : '#A51A1A', transform: 'translateY(-1px)' },
          },
          text: { color: c.textSecondary, '&:hover': { background: c.grayBg, color: c.text } },
        },
      },
      MuiIconButton: {
        styleOverrides: {
          root: {
            transition: 'all 0.2s ease',
            borderRadius: 10,
            color: c.textSecondary,
            '&:hover': { background: c.grayBg, color: c.text },
          },
        },
      },
      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: 12,
            backgroundColor: c.inputBg,
            minHeight: 46,
            transition: 'border-color 0.2s ease, box-shadow 0.2s ease',
            '& .MuiOutlinedInput-notchedOutline': { borderColor: c.border },
            '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: c.borderStrong },
            '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
              borderColor: c.primary,
              borderWidth: 2,
            },
            '&.Mui-focused': { boxShadow: `0 0 0 4px ${mode === 'dark' ? 'rgba(63, 191, 134, 0.15)' : 'rgba(21, 115, 71, 0.10)'}` },
          },
          sizeSmall: { minHeight: 38, borderRadius: 10 },
          input: { paddingTop: 11, paddingBottom: 11 },
          inputSizeSmall: { paddingTop: 8, paddingBottom: 8 },
        },
      },
      MuiInputLabel: {
        styleOverrides: {
          root: { color: c.textSecondary, '&.Mui-focused': { color: c.primary } },
        },
      },
      MuiCard: {
        styleOverrides: {
          root: {
            borderRadius: 16,
            border: `1px solid ${c.border}`,
            boxShadow: mode === 'dark' ? 'none' : softShadow,
            backgroundImage: 'none',
          },
        },
      },
      MuiPaper: {
        styleOverrides: {
          root: { backgroundImage: 'none' },
          outlined: { borderColor: c.border },
        },
      },
      MuiTableContainer: {
        styleOverrides: {
          root: {
            borderRadius: 16,
            border: `1px solid ${c.border}`,
            overflow: 'hidden',
            boxShadow: mode === 'dark' ? 'none' : softShadow,
            backgroundColor: c.card,
          },
        },
      },
      MuiTable: {
        styleOverrides: { root: { borderCollapse: 'separate' } },
      },
      MuiTableHead: {
        styleOverrides: {
          root: {
            '& .MuiTableCell-head': {
              backgroundColor: c.primarySoft,
              color: c.primary,
              fontWeight: 700,
              fontSize: 13,
              letterSpacing: '0.02em',
              textTransform: 'uppercase',
              borderBottom: `1px solid ${c.border}`,
            },
          },
        },
      },
      MuiTableCell: {
        styleOverrides: {
          root: {
            padding: '14px 16px',
            borderBottom: `1px solid ${mode === 'dark' ? '#242C36' : '#EEF1F4'}`,
            fontSize: 14,
            color: c.text,
          },
          head: { borderBottom: `1px solid ${c.border}` },
        },
      },
      MuiTableRow: {
        styleOverrides: {
          root: {
            transition: 'background-color 0.15s ease',
            '&:last-child .MuiTableCell-root': { borderBottom: 'none' },
            '&:hover': { backgroundColor: c.tableRowHover },
          },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: { borderRadius: 8, fontWeight: 600, height: 26 },
          sizeSmall: { height: 24, fontSize: 12 },
        },
      },
      MuiDialog: {
        styleOverrides: {
          paper: {
            borderRadius: 20,
            boxShadow: mode === 'dark' ? '0 20px 50px rgba(0, 0, 0, 0.5)' : '0 20px 50px rgba(16, 24, 40, 0.18)',
            border: `1px solid ${c.border}`,
          },
        },
      },
      MuiBackdrop: {
        styleOverrides: {
          root: { backdropFilter: 'blur(4px)', backgroundColor: mode === 'dark' ? 'rgba(0, 0, 0, 0.55)' : 'rgba(15, 23, 42, 0.28)' },
        },
      },
      MuiDialogTitle: { styleOverrides: { root: { fontWeight: 700, fontSize: 20, padding: '24px 28px 8px' } } },
      MuiDialogContent: { styleOverrides: { root: { padding: '16px 28px' } } },
      MuiDialogActions: { styleOverrides: { root: { padding: '16px 28px 24px', gap: 8 } } },
      MuiMenu: {
        styleOverrides: { paper: { borderRadius: 14, boxShadow: '0 12px 32px rgba(16, 24, 40, 0.14)', marginTop: 6 } },
      },
      MuiMenuItem: {
        styleOverrides: { root: { borderRadius: 8, padding: '8px 12px', margin: '2px 6px', minHeight: 36 } },
      },
      MuiListItemButton: { styleOverrides: { root: { borderRadius: 10 } } },
      MuiAppBar: {
        styleOverrides: {
          root: { backgroundColor: c.appBarBg, color: c.text, boxShadow: 'none' },
        },
      },
      MuiToolbar: {
        styleOverrides: { root: { minHeight: 72 } },
      },
      MuiAvatar: {
        styleOverrides: {
          root: { backgroundColor: c.primaryLight, color: c.primary, fontWeight: 700 },
        },
      },
      MuiTab: { styleOverrides: { root: { textTransform: 'none', fontWeight: 600 } } },
      MuiAlert: {
        styleOverrides: { root: { borderRadius: 12 } },
      },
    },
  };

  return createTheme(themeOptions);
}

export const lightTheme = createAppTheme('light');
export const darkTheme = createAppTheme('dark');
