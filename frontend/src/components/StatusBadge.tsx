import { Box, useTheme } from '@mui/material';
import type { ReactNode } from 'react';

type StatusVariant = 'success' | 'info' | 'warning' | 'error' | 'neutral';

const lightStyles: Record<StatusVariant, { bg: string; fg: string; dot: string }> = {
  success: { bg: '#E6F4EC', fg: '#0F5A37', dot: '#22A06B' },
  info: { bg: '#E8F0FE', fg: '#1A56DB', dot: '#3B82F6' },
  warning: { bg: '#FEF3C7', fg: '#92610E', dot: '#F59E0B' },
  error: { bg: '#FDECEC', fg: '#A51A1A', dot: '#EF4444' },
  neutral: { bg: '#F3F4F6', fg: '#4B5563', dot: '#9CA3AF' },
};

const darkStyles: Record<StatusVariant, { bg: string; fg: string; dot: string }> = {
  success: { bg: '#123B2B', fg: '#57D09A', dot: '#3FBF86' },
  info: { bg: '#1B2A4A', fg: '#7AA2FF', dot: '#5B8DEF' },
  warning: { bg: '#3A2E14', fg: '#E8A33D', dot: '#F0B24B' },
  error: { bg: '#3A1D1D', fg: '#F4716B', dot: '#F87171' },
  neutral: { bg: '#232B35', fg: '#B6BEC9', dot: '#9CA3AF' },
};

export function statusVariant(status?: string): StatusVariant {
  switch (status) {
    case 'Payee':
      return 'success';
    case 'Finalisee':
      return 'info';
    case 'Annulee':
      return 'error';
    default:
      return 'neutral';
  }
}

interface StatusBadgeProps {
  label: ReactNode;
  variant?: StatusVariant;
}

export default function StatusBadge({ label, variant = 'neutral' }: StatusBadgeProps) {
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';
  const v = (isDark ? darkStyles : lightStyles)[variant];
  return (
    <Box
      component="span"
      sx={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 0.75,
        backgroundColor: v.bg,
        color: v.fg,
        borderRadius: '999px',
        padding: '3px 10px',
        fontSize: 12.5,
        fontWeight: 700,
        lineHeight: 1.4,
        whiteSpace: 'nowrap',
      }}
    >
      <Box
        component="span"
        sx={{ width: 7, height: 7, borderRadius: '50%', backgroundColor: v.dot, flexShrink: 0 }}
      />
      {label}
    </Box>
  );
}
