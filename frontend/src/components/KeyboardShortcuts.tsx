import { useEffect, useMemo, useState } from 'react';
import { Box, Divider, Typography, useTheme } from '@mui/material';
import { Keyboard as KeyboardIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { COMMAND_META, GROUP_ORDER, GROUP_LABEL_KEY } from '../utils/commandMeta';
import { formatShortcut } from '../stores/shortcutsStore';
import type { Bindings, CommandId, ShortcutBinding } from '../stores/shortcutsStore';
import ShortcutRecorder from './ShortcutRecorder';

interface KeyboardShortcutsProps {
  bindings: Bindings;
  onChange: (id: CommandId, b: ShortcutBinding) => void;
  onReset: (id: CommandId) => void;
  onResetAll: () => void;
  onConflict: (id: CommandId) => void;
}

interface KeyDef {
  label: string;
  code: string;
  w?: number;
}

const ROWS: KeyDef[][] = [
  [
    { label: 'Échap', code: 'Escape' },
    { label: '1', code: 'Digit1' },
    { label: '2', code: 'Digit2' },
    { label: '3', code: 'Digit3' },
    { label: '4', code: 'Digit4' },
    { label: '5', code: 'Digit5' },
    { label: '6', code: 'Digit6' },
    { label: '7', code: 'Digit7' },
    { label: '8', code: 'Digit8' },
    { label: '9', code: 'Digit9' },
    { label: '0', code: 'Digit0' },
    { label: '−', code: 'Minus' },
    { label: '⌫', code: 'Backspace', w: 1.6 },
  ],
  [
    { label: 'Tab', code: 'Tab', w: 1.5 },
    { label: 'Q', code: 'KeyQ' },
    { label: 'W', code: 'KeyW' },
    { label: 'E', code: 'KeyE' },
    { label: 'R', code: 'KeyR' },
    { label: 'T', code: 'KeyT' },
    { label: 'Y', code: 'KeyY' },
    { label: 'U', code: 'KeyU' },
    { label: 'I', code: 'KeyI' },
    { label: 'O', code: 'KeyO' },
    { label: 'P', code: 'KeyP' },
    { label: '[', code: 'BracketLeft' },
    { label: ']', code: 'BracketRight' },
  ],
  [
    { label: 'Verr', code: 'CapsLock', w: 1.5 },
    { label: 'A', code: 'KeyA' },
    { label: 'S', code: 'KeyS' },
    { label: 'D', code: 'KeyD' },
    { label: 'F', code: 'KeyF' },
    { label: 'G', code: 'KeyG' },
    { label: 'H', code: 'KeyH' },
    { label: 'J', code: 'KeyJ' },
    { label: 'K', code: 'KeyK' },
    { label: 'L', code: 'KeyL' },
    { label: ';', code: 'Semicolon' },
    { label: "'", code: 'Quote' },
    { label: '⏎', code: 'Enter', w: 1.7 },
  ],
  [
    { label: 'Maj', code: 'ShiftLeft', w: 2 },
    { label: 'Z', code: 'KeyZ' },
    { label: 'X', code: 'KeyX' },
    { label: 'C', code: 'KeyC' },
    { label: 'V', code: 'KeyV' },
    { label: 'B', code: 'KeyB' },
    { label: 'N', code: 'KeyN' },
    { label: 'M', code: 'KeyM' },
    { label: ',', code: 'Comma' },
    { label: '.', code: 'Period' },
    { label: '/', code: 'Slash' },
    { label: 'Maj', code: 'ShiftRight', w: 2 },
  ],
  [
    { label: 'Ctrl', code: 'ControlLeft', w: 1.3 },
    { label: 'Win', code: 'MetaLeft', w: 1.3 },
    { label: 'Alt', code: 'AltLeft', w: 1.3 },
    { label: 'Espace', code: 'Space', w: 4.2 },
    { label: 'Alt', code: 'AltRight', w: 1.3 },
    { label: 'Ctrl', code: 'ControlRight', w: 1.3 },
  ],
];

const KEY_SIZE = 26;

function modifierCodes(b: ShortcutBinding | undefined): string[] {
  if (!b) return [];
  const out: string[] = [b.code];
  if (b.ctrl) out.push('ControlLeft', 'ControlRight');
  if (b.alt) out.push('AltLeft', 'AltRight');
  if (b.shift) out.push('ShiftLeft', 'ShiftRight');
  if (b.meta) out.push('MetaLeft', 'MetaRight');
  return out;
}

export default function KeyboardShortcuts({
  bindings,
  onChange,
  onReset,
  onResetAll,
  onConflict,
}: KeyboardShortcutsProps) {
  const { t } = useTranslation();
  const theme = useTheme();
  const [selected, setSelected] = useState<CommandId | null>(null);
  const [pressed, setPressed] = useState<Set<string>>(new Set());

  useEffect(() => {
    const down = (e: KeyboardEvent) => {
      setPressed((prev) => {
        const next = new Set(prev);
        next.add(e.code);
        return next;
      });
    };
    const up = (e: KeyboardEvent) => {
      setPressed((prev) => {
        const next = new Set(prev);
        next.delete(e.code);
        return next;
      });
    };
    window.addEventListener('keydown', down);
    window.addEventListener('keyup', up);
    return () => {
      window.removeEventListener('keydown', down);
      window.removeEventListener('keyup', up);
    };
  }, []);

  const usedCodes = useMemo(() => {
    const s = new Set<string>();
    for (const b of Object.values(bindings)) s.add(b.code);
    return s;
  }, [bindings]);

  const selectedBinding = selected ? bindings[selected] : undefined;
  const selectedCodes = useMemo(() => new Set(modifierCodes(selectedBinding)), [selectedBinding]);

  const selectedMeta = selected ? COMMAND_META.find((c) => c.id === selected) : undefined;

  const keyClass = (code: string) => {
    const cls: string[] = ['keycap'];
    if (usedCodes.has(code)) cls.push('used');
    if (selectedCodes.has(code)) cls.push('selected');
    if (pressed.has(code)) cls.push('pressed');
    return cls.join(' ');
  };

  return (
    <Box>
      {/* Command list */}
      <Box sx={{ mb: 1.5 }}>
        {GROUP_ORDER.map((group) => {
          const cmds = COMMAND_META.filter((c) => c.group === group);
          return (
            <Box key={group} sx={{ mb: 0.5 }}>
              <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.5 }}>
                {t(GROUP_LABEL_KEY[group])}
              </Typography>
              <Box>
                {cmds.map((cmd) => (
                  <Box
                    key={cmd.id}
                    onClick={() => setSelected(cmd.id)}
                    sx={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: 1,
                      py: 0.25,
                      px: 0.5,
                      borderRadius: 1,
                      cursor: 'pointer',
                      bgcolor: selected === cmd.id ? 'action.selected' : 'transparent',
                      '&:hover': { bgcolor: 'action.hover' },
                    }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: selected === cmd.id ? 700 : 500 }}>
                      {t(cmd.labelKey)}
                    </Typography>
                    <ShortcutRecorder
                      commandId={cmd.id}
                      binding={bindings[cmd.id]}
                      allBindings={bindings}
                      onChange={(b) => onChange(cmd.id, b)}
                      onReset={() => onReset(cmd.id)}
                      onConflict={onConflict}
                    />
                  </Box>
                ))}
              </Box>
            </Box>
          );
        })}
      </Box>

      <Divider sx={{ my: 1 }} />

      {/* Keyboard visual */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
        <KeyboardIcon size={16} style={{ color: theme.palette.primary.main }} />
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          {t('options.keyboard.centerTitle')}
        </Typography>
      </Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
        {t('options.keyboard.centerDesc')}
      </Typography>

      <Box
        sx={{
          display: 'inline-flex',
          flexDirection: 'column',
          gap: 0.5,
          p: 1,
          borderRadius: 2,
          background: 'linear-gradient(180deg, #ffffff 0%, #eef2f6 100%)',
          boxShadow: '0 6px 18px rgba(15,23,42,0.12), inset 0 1px 0 rgba(255,255,255,0.9)',
          border: '1px solid',
          borderColor: 'divider',
          '& .keycap': {
            height: KEY_SIZE,
            minWidth: KEY_SIZE,
            px: 0.5,
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 11,
            fontWeight: 600,
            color: '#1f2937',
            background: 'linear-gradient(180deg,#ffffff,#dfe5ec)',
            border: '1px solid #c4ccd6',
            borderRadius: 1.5,
            boxShadow: '0 2px 0 #b7c0cc, 0 3px 4px rgba(15,23,42,0.12)',
            transition: 'transform 0.04s ease, box-shadow 0.04s ease, background 0.1s ease',
            userSelect: 'none',
          },
          '& .keycap.used': {
            borderBottom: `2px solid ${theme.palette.primary.main}`,
            color: theme.palette.primary.dark,
          },
          '& .keycap.selected': {
            background: `linear-gradient(180deg, ${theme.palette.primary.light}, ${theme.palette.primary.main})`,
            color: '#fff',
            borderColor: theme.palette.primary.dark,
            boxShadow: `0 0 0 2px ${theme.palette.primary.main}55, 0 2px 0 ${theme.palette.primary.dark}`,
          },
          '& .keycap.pressed': {
            transform: 'translateY(2px)',
            boxShadow: '0 0 0 #b7c0cc, 0 1px 2px rgba(15,23,42,0.18)',
            background: 'linear-gradient(180deg,#e9eef4,#cfd7e1)',
          },
        }}
      >
        {ROWS.map((row, ri) => (
          <Box key={ri} sx={{ display: 'flex', gap: 0.5 }}>
            {row.map((k) => (
              <Box
                key={k.code}
                className={keyClass(k.code)}
                sx={{ width: `${(k.w ?? 1) * (KEY_SIZE + 4) - 4}px` }}
              >
                {k.label}
              </Box>
            ))}
          </Box>
        ))}
      </Box>

      {/* Selected command info */}
      <Box
        sx={{
          mt: 1,
          minHeight: 34,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 1,
          px: 1.25,
          py: 0.5,
          borderRadius: 1.5,
          border: '1px dashed',
          borderColor: selected ? 'primary.main' : 'divider',
          background: selected ? 'action.selected' : 'transparent',
        }}
      >
        <Typography variant="body2">
          {selectedMeta ? (
            <>
              <Box component="span" sx={{ fontWeight: 700, color: 'primary.main' }}>
                {t(selectedMeta.labelKey)}
              </Box>{' '}
              <Box component="span" sx={{ color: 'text.secondary', fontFamily: 'monospace' }}>
                {selectedBinding ? formatShortcut(selectedBinding) : ''}
              </Box>
            </>
          ) : (
            <Box component="span" color="text.secondary">
              {t('options.keyboard.hint')}
            </Box>
          )}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {selectedMeta ? t('options.keyboard.selected') : ''}
        </Typography>
      </Box>

      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 1 }}>
        <Typography
          component="span"
          onClick={onResetAll}
          sx={{
            fontSize: 12,
            color: 'primary.main',
            cursor: 'pointer',
            fontWeight: 600,
            '&:hover': { textDecoration: 'underline' },
          }}
        >
          {t('options.keyboard.resetAll')}
        </Typography>
      </Box>
    </Box>
  );
}
