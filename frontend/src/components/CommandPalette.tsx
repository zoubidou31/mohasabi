import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Box,
  Dialog,
  List,
  ListItemButton,
  ListItemText,
  TextField,
  Typography,
} from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { COMMAND_IDS, invokeCommand, setShortcutPaletteOpen } from '../utils/shortcuts';
import { formatShortcut } from '../stores/shortcutsStore';
import { useShortcutsStore } from '../stores/shortcutsStore';
import type { CommandGroup } from '../utils/commandMeta';
import { GROUP_LABEL_KEY } from '../utils/commandMeta';

interface PaletteItem {
  id: string;
  labelKey: string;
  group: CommandGroup;
  run: () => void;
}

interface CommandPaletteProps {
  open: boolean;
  onClose: () => void;
}

export default function CommandPalette({ open, onClose }: CommandPaletteProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const bindings = useShortcutsStore((s) => s.bindings);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  // Liste curée des commandes ouvrables depuis le palette.
  const items: PaletteItem[] = useMemo(
    () => [
      { id: COMMAND_IDS.NEW, labelKey: 'options.shortcuts.commands.newInvoice', group: 'invoices', run: () => navigate('/invoices/new') },
      { id: COMMAND_IDS.NEW, labelKey: 'options.shortcuts.commands.newClient', group: 'clients', run: () => navigate('/clients') },
      { id: COMMAND_IDS.NEW, labelKey: 'options.shortcuts.commands.newProduct', group: 'products', run: () => navigate('/products') },
      { id: COMMAND_IDS.SAVE, labelKey: 'options.shortcuts.commands.save', group: 'global', run: () => invokeCommand(COMMAND_IDS.SAVE) },
      { id: COMMAND_IDS.GLOBAL_SEARCH, labelKey: 'options.shortcuts.commands.globalSearch', group: 'global', run: () => {} },
      { id: COMMAND_IDS.HELP, labelKey: 'options.shortcuts.commands.help', group: 'global', run: () => invokeCommand(COMMAND_IDS.HELP) },
      { id: COMMAND_IDS.NAV_DASHBOARD, labelKey: 'options.shortcuts.commands.navDashboard', group: 'global', run: () => navigate('/invoices') },
      { id: COMMAND_IDS.NAV_INVOICES, labelKey: 'options.shortcuts.commands.navInvoices', group: 'global', run: () => navigate('/invoices') },
      { id: COMMAND_IDS.NAV_CLIENTS, labelKey: 'options.shortcuts.commands.navClients', group: 'global', run: () => navigate('/clients') },
      { id: COMMAND_IDS.NAV_PRODUCTS, labelKey: 'options.shortcuts.commands.navProducts', group: 'global', run: () => navigate('/products') },
      { id: COMMAND_IDS.NAV_REPORTS, labelKey: 'options.shortcuts.commands.navReports', group: 'global', run: () => navigate('/reports') },
      { id: COMMAND_IDS.NAV_SETTINGS, labelKey: 'options.shortcuts.commands.navSettings', group: 'global', run: () => navigate('/options') },
      { id: COMMAND_IDS.FOCUS_SEARCH, labelKey: 'options.shortcuts.commands.focusSearch', group: 'global', run: () => invokeCommand(COMMAND_IDS.FOCUS_SEARCH) },
      { id: COMMAND_IDS.FINALIZE, labelKey: 'options.shortcuts.commands.finalize', group: 'invoices', run: () => invokeCommand(COMMAND_IDS.FINALIZE) },
      { id: COMMAND_IDS.PRINT, labelKey: 'options.shortcuts.commands.print', group: 'invoices', run: () => invokeCommand(COMMAND_IDS.PRINT) },
      { id: COMMAND_IDS.DUPLICATE, labelKey: 'options.shortcuts.commands.duplicate', group: 'invoices', run: () => invokeCommand(COMMAND_IDS.DUPLICATE) },
      { id: COMMAND_IDS.EXPORT, labelKey: 'options.shortcuts.commands.export', group: 'reports', run: () => invokeCommand(COMMAND_IDS.EXPORT) },
      { id: COMMAND_IDS.OPEN_SELECTED, labelKey: 'options.shortcuts.commands.openSelected', group: 'clients', run: () => invokeCommand(COMMAND_IDS.OPEN_SELECTED) },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [navigate],
  );

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return items;
    return items.filter((it) => t(it.labelKey).toLowerCase().includes(q));
  }, [items, query, t]);

  useEffect(() => {
    setShortcutPaletteOpen(open);
    if (open) {
      setQuery('');
      setActive(0);
      // Focus après ouverture (le Dialog monte un frame plus tard).
      const id = window.setTimeout(() => inputRef.current?.focus(), 30);
      return () => window.clearTimeout(id);
    }
    return undefined;
  }, [open]);

  useEffect(() => {
    setActive(0);
  }, [query]);

  const runItem = (item: PaletteItem | undefined) => {
    if (!item) return;
    item.run();
    onClose();
  };

  const onInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActive((a) => Math.min(a + 1, filtered.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActive((a) => Math.max(a - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      runItem(filtered[active]);
    } else if (e.key === 'Escape') {
      e.preventDefault();
      onClose();
    } else if (e.ctrlKey && (e.key === 'k' || e.key === 'K')) {
      e.preventDefault();
      onClose();
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      BackdropProps={{ style: { backgroundColor: 'rgba(0,0,0,0.45)' } }}
      PaperProps={{
        sx: { width: '100%', maxWidth: 560, borderRadius: 3, overflow: 'hidden' },
      }}
      sx={{ '& .MuiDialog-paper': { mt: '-20vh' } }}
    >
      <Box sx={{ p: 1.5, borderBottom: '1px solid', borderColor: 'divider' }}>
        <TextField
          inputRef={inputRef}
          fullWidth
          variant="standard"
          placeholder={t('options.shortcuts.palettePlaceholder')}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={onInputKeyDown}
          InputProps={{ disableUnderline: true, sx: { fontSize: 16 } }}
        />
      </Box>
      <List dense sx={{ maxHeight: 360, overflowY: 'auto', py: 0.5 }}>
        {filtered.length === 0 && (
          <Typography sx={{ p: 2, color: 'text.secondary', textAlign: 'center' }}>
            {t('common.none')}
          </Typography>
        )}
        {filtered.map((it, idx) => (
          <ListItemButton
            key={`${it.id}-${it.labelKey}-${idx}`}
            selected={idx === active}
            onMouseEnter={() => setActive(idx)}
            onClick={() => runItem(it)}
          >
            <ListItemText
              primary={t(it.labelKey)}
              secondary={t(GROUP_LABEL_KEY[it.group])}
              primaryTypographyProps={{ fontWeight: idx === active ? 700 : 500 }}
            />
            <Box
              component="span"
              sx={{ fontFamily: 'monospace', fontSize: 12, color: 'text.secondary', whiteSpace: 'nowrap' }}
            >
              {formatShortcut(bindings[it.id as keyof typeof bindings])}
            </Box>
          </ListItemButton>
        ))}
      </List>
    </Dialog>
  );
}
