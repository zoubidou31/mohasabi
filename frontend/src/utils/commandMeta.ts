import type { CommandId } from '../stores/shortcutsStore';

export type CommandGroup = 'global' | 'invoices' | 'clients' | 'products' | 'reports';

export interface CommandMeta {
  id: CommandId;
  labelKey: string;
  group: CommandGroup;
}

/** Ordre d'affichage des groupes dans la page Options → Raccourcis. */
export const GROUP_ORDER: CommandGroup[] = ['global', 'invoices', 'clients', 'products', 'reports'];

export const GROUP_LABEL_KEY: Record<CommandGroup, string> = {
  global: 'options.shortcuts.groups.global',
  invoices: 'options.shortcuts.groups.invoices',
  clients: 'options.shortcuts.groups.clients',
  products: 'options.shortcuts.groups.products',
  reports: 'options.shortcuts.groups.reports',
};

/**
 * Métadonnées de toutes les commandes raccourcissables. Source unique de vérité
 * pour la page Options (liste groupée) et le Command Palette (libellés/touches).
 */
export const COMMAND_META: CommandMeta[] = [
  // Global
  { id: 'new', labelKey: 'options.shortcuts.commands.new', group: 'global' },
  { id: 'save', labelKey: 'options.shortcuts.commands.save', group: 'global' },
  { id: 'global-search', labelKey: 'options.shortcuts.commands.globalSearch', group: 'global' },
  { id: 'help', labelKey: 'options.shortcuts.commands.help', group: 'global' },
  { id: 'esc', labelKey: 'options.shortcuts.commands.esc', group: 'global' },
  { id: 'nav-dashboard', labelKey: 'options.shortcuts.commands.navDashboard', group: 'global' },
  { id: 'nav-invoices', labelKey: 'options.shortcuts.commands.navInvoices', group: 'global' },
  { id: 'nav-clients', labelKey: 'options.shortcuts.commands.navClients', group: 'global' },
  { id: 'nav-products', labelKey: 'options.shortcuts.commands.navProducts', group: 'global' },
  { id: 'nav-reports', labelKey: 'options.shortcuts.commands.navReports', group: 'global' },
  { id: 'nav-settings', labelKey: 'options.shortcuts.commands.navSettings', group: 'global' },
  { id: 'nav-back', labelKey: 'options.shortcuts.commands.navBack', group: 'global' },
  { id: 'nav-forward', labelKey: 'options.shortcuts.commands.navForward', group: 'global' },
  { id: 'focus-search', labelKey: 'options.shortcuts.commands.focusSearch', group: 'global' },

  // Factures
  { id: 'finalize', labelKey: 'options.shortcuts.commands.finalize', group: 'invoices' },
  { id: 'print', labelKey: 'options.shortcuts.commands.print', group: 'invoices' },
  { id: 'duplicate', labelKey: 'options.shortcuts.commands.duplicate', group: 'invoices' },

  // Clients
  { id: 'open-selected', labelKey: 'options.shortcuts.commands.openSelected', group: 'clients' },

  // Produits
  // (new / save / focus-search already covered globally)

  // Rapports
  { id: 'export', labelKey: 'options.shortcuts.commands.export', group: 'reports' },
];
