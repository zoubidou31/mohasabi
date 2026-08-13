import { useState } from 'react';
import { Box, Button, IconButton, Tooltip } from '@mui/material';
import { Pencil, RotateCcw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { findConflict, formatShortcut, isModifierCode } from '../stores/shortcutsStore';
import type { Bindings, CommandId, ShortcutBinding } from '../stores/shortcutsStore';

interface ShortcutRecorderProps {
  commandId: CommandId;
  /** Binding courant (provenant du draft, pas du store). */
  binding: ShortcutBinding;
  /** Ensemble des bindings du draft, utilisé pour la détection de conflit. */
  allBindings: Bindings;
  /** Applique une nouvelle combinaison dans le draft uniquement (aucune persistance). */
  onChange: (binding: ShortcutBinding) => void;
  /** Rétablit le défaut dans le draft uniquement. */
  onReset: () => void;
  /** Signale un conflit avec un autre commandId (aucune persistance). */
  onConflict?: (conflictingId: CommandId) => void;
  disabled?: boolean;
}

export default function ShortcutRecorder({
  commandId,
  binding,
  allBindings,
  onChange,
  onReset,
  onConflict,
  disabled,
}: ShortcutRecorderProps) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(false);
  const [preview, setPreview] = useState<ShortcutBinding | null>(null);

  const onKeyDown = (e: React.KeyboardEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.key === 'Escape') {
      setEditing(false);
      setPreview(null);
      return;
    }
    if (isModifierCode(e.code)) {
      setPreview({ code: '', ctrl: e.ctrlKey, alt: e.altKey, shift: e.shiftKey, meta: e.metaKey });
      return;
    }
    if (!e.code) return;
    const candidate: ShortcutBinding = {
      code: e.code,
      ctrl: e.ctrlKey,
      alt: e.altKey,
      shift: e.shiftKey,
      meta: e.metaKey,
    };
    const conflictId = findConflict(allBindings, commandId, candidate);
    if (conflictId) {
      onConflict?.(conflictId);
      setEditing(false);
      setPreview(null);
      return;
    }
    onChange(candidate);
    setEditing(false);
    setPreview(null);
  };

  if (editing) {
    return (
      <Box
        tabIndex={0}
        autoFocus
        onKeyDown={onKeyDown}
        onBlur={() => {
          setEditing(false);
          setPreview(null);
        }}
        sx={{
          minWidth: 132,
          px: 1.5,
          py: 0.5,
          borderRadius: 1.5,
          border: '1px dashed',
          borderColor: 'primary.main',
          bgcolor: 'action.selected',
          color: 'text.secondary',
          fontSize: 13,
          fontFamily: 'monospace',
          outline: 'none',
          textAlign: 'center',
        }}
      >
        {preview && preview.code ? formatShortcut(preview) : t('options.shortcuts.pressKey')}
      </Box>
    );
  }

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
      <Button
        size="small"
        variant="outlined"
        disabled={disabled}
        onClick={() => setEditing(true)}
        sx={{ fontFamily: 'monospace', minWidth: 116, textTransform: 'none' }}
      >
        {formatShortcut(binding) || '—'}
      </Button>
      <Tooltip title={t('options.shortcuts.change')}>
        <IconButton
          size="small"
          disabled={disabled}
          onClick={() => setEditing(true)}
          aria-label={t('options.shortcuts.change')}
        >
          <Pencil size={14} />
        </IconButton>
      </Tooltip>
      <Tooltip title={t('options.shortcuts.resetOne')}>
        <IconButton size="small" disabled={disabled} onClick={onReset} aria-label={t('options.shortcuts.resetOne')}>
          <RotateCcw size={14} />
        </IconButton>
      </Tooltip>
    </Box>
  );
}
