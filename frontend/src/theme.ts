import { createTheme } from '@mui/material/styles';

export const colors = {
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
};

const softShadow =
  '0 1px 2px rgba(16, 24, 40, 0.04), 0 1px 3px rgba(16, 24, 40, 0.06)';

export const lightTheme = createTheme({
  direction: 'ltr',
  palette: {
    mode: 'light',
    primary: { main: colors.primary, dark: colors.primaryDark, light: colors.primaryLight, contrastText: '#FFFFFF' },
    secondary: { main: '#4F46E5' },
    success: { main: colors.success },
    info: { main: colors.info },
    warning: { main: '#D97706' },
    error: { main: colors.error },
    background: { default: colors.background, paper: colors.card },
    text: { primary: colors.text, secondary: colors.textSecondary },
    divider: colors.border,
  },
  shape: { borderRadius: 16 },
  typography: {
    fontFamily:
      "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif",
    h4: { fontWeight: 800, letterSpacing: '-0.02em' },
    h5: { fontWeight: 800, letterSpacing: '-0.01em' },
    h6: { fontWeight: 700 },
    button: { textTransform: 'none', fontWeight: 600, letterSpacing: '0.01em' },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          backgroundColor: colors.background,
          color: colors.text,
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
          background: colors.primary,
          boxShadow: '0 4px 12px rgba(21, 115, 71, 0.25)',
          '&:hover': {
            background: colors.primaryDark,
            boxShadow: '0 6px 16px rgba(21, 115, 71, 0.32)',
            transform: 'translateY(-1px)',
          },
        },
        outlined: {
          borderColor: colors.border,
          color: colors.text,
          '&:hover': { borderColor: '#C9D2DA', background: '#F8FAFB' },
        },
        outlinedPrimary: { borderColor: colors.primary, color: colors.primary },
        containedError: {
          background: colors.error,
          boxShadow: '0 4px 12px rgba(200, 30, 30, 0.22)',
          '&:hover': { background: '#A51A1A', transform: 'translateY(-1px)' },
        },
        text: { color: colors.textSecondary, '&:hover': { background: '#F3F4F6', color: colors.text } },
      },
    },
    MuiIconButton: {
      styleOverrides: {
        root: {
          transition: 'all 0.2s ease',
          borderRadius: 10,
          color: colors.textSecondary,
          '&:hover': { background: colors.grayBg, color: colors.text },
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 12,
          backgroundColor: '#FFFFFF',
          minHeight: 46,
          transition: 'border-color 0.2s ease, box-shadow 0.2s ease',
          '& .MuiOutlinedInput-notchedOutline': { borderColor: colors.border },
          '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: '#C9D2DA' },
          '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
            borderColor: colors.primary,
            borderWidth: 2,
          },
          '&.Mui-focused': { boxShadow: '0 0 0 4px rgba(21, 115, 71, 0.10)' },
        },
        sizeSmall: { minHeight: 38, borderRadius: 10 },
        input: { paddingTop: 11, paddingBottom: 11 },
        inputSizeSmall: { paddingTop: 8, paddingBottom: 8 },
      },
    },
    MuiInputLabel: {
      styleOverrides: {
        root: { color: colors.textSecondary, '&.Mui-focused': { color: colors.primary } },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: 16,
          border: `1px solid ${colors.border}`,
          boxShadow: softShadow,
          backgroundImage: 'none',
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
        },
        outlined: { borderColor: colors.border },
      },
    },
    MuiTableContainer: {
      styleOverrides: {
        root: {
          borderRadius: 16,
          border: `1px solid ${colors.border}`,
          overflow: 'hidden',
          boxShadow: softShadow,
          backgroundColor: '#FFFFFF',
        },
      },
    },
    MuiTable: {
      styleOverrides: {
        root: { borderCollapse: 'separate' },
      },
    },
    MuiTableHead: {
      styleOverrides: {
        root: {
          '& .MuiTableCell-head': {
            backgroundColor: colors.primarySoft,
            color: '#0F5132',
            fontWeight: 700,
            fontSize: 13,
            letterSpacing: '0.02em',
            textTransform: 'uppercase',
            borderBottom: `1px solid ${colors.border}`,
          },
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        root: {
          padding: '14px 16px',
          borderBottom: '1px solid #EEF1F4',
          fontSize: 14,
          color: colors.text,
        },
        head: { borderBottom: `1px solid ${colors.border}` },
      },
    },
    MuiTableRow: {
      styleOverrides: {
        root: {
          transition: 'background-color 0.15s ease',
          '&:last-child .MuiTableCell-root': { borderBottom: 'none' },
          '&:hover': { backgroundColor: '#F6FAF8' },
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
          boxShadow: '0 20px 50px rgba(16, 24, 40, 0.18)',
          border: `1px solid ${colors.border}`,
        },
      },
    },
    MuiBackdrop: {
      styleOverrides: {
        root: { backdropFilter: 'blur(4px)', backgroundColor: 'rgba(15, 23, 42, 0.28)' },
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
        root: { backgroundColor: '#FFFFFF', color: colors.text, boxShadow: 'none' },
      },
    },
    MuiToolbar: {
      styleOverrides: {
        root: { minHeight: 72 },
      },
    },
    MuiAvatar: {
      styleOverrides: {
        root: { backgroundColor: colors.primaryLight, color: colors.primary, fontWeight: 700 },
      },
    },
    MuiTab: { styleOverrides: { root: { textTransform: 'none', fontWeight: 600 } } },
    MuiAlert: {
      styleOverrides: {
        root: { borderRadius: 12 },
      },
    },
  },
});

export const darkTheme = createTheme({
  direction: 'ltr',
  palette: {
    mode: 'dark',
    primary: { main: '#3FBF86' },
    secondary: { main: '#818CF8' },
    background: { default: '#0F1419', paper: '#1A2029' },
    text: { primary: '#F3F4F6', secondary: '#9CA3AF' },
    divider: '#2A323D',
  },
});
