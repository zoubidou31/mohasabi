import { Box } from '@mui/material';
import type { ReactNode } from 'react';

type StatusVariant = 'success' | 'info' | 'warning' | 'error' | 'neutral';

const variantStyles: Record<StatusVariant, { bg: string; fg: string; dot: string }> = {
  success: { bg: '#E6F4EC', fg: '#0F5A37', dot: '#22A06B' },
  info: { bg: '#E8F0FE', fg: '#1A56DB', dot: '#3B82F6' },
  warning: { bg: '#FEF3C7', fg: '#92610E', dot: '#F59E0B' },
  error: { bg: '#FDECEC', fg: '#A51A1A', dot: '#EF4444' },
  neutral: { bg: '#F3F4F6', fg: '#4B5563', dot: '#9CA3AF' },
};

export function statusVariant(status?: string): StatusVariant {
  switch (status) {
    case 'Payee':
      return 'success';
    case 'Finalisee':
      return 'info';
    case 'En attente':
      return 'warning';
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
  const v = variantStyles[variant];
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
