import { create } from 'zustand';

export const COMMAND_IDS = {
  NEW: 'new',
  SAVE: 'save',
  GLOBAL_SEARCH: 'global-search',
  HELP: 'help',
  ESC: 'esc',
  NAV_DASHBOARD: 'nav-dashboard',
  NAV_INVOICES: 'nav-invoices',
  NAV_CLIENTS: 'nav-clients',
  NAV_PRODUCTS: 'nav-products',
  NAV_REPORTS: 'nav-reports',
  NAV_SETTINGS: 'nav-settings',
  NAV_BACK: 'nav-back',
  NAV_FORWARD: 'nav-forward',
  FOCUS_SEARCH: 'focus-search',
  FINALIZE: 'finalize',
  PRINT: 'print',
  DUPLICATE: 'duplicate',
  EXPORT: 'export',
  OPEN_SELECTED: 'open-selected',
} as const;

export type CommandId = (typeof COMMAND_IDS)[keyof typeof COMMAND_IDS];

export interface ShortcutBinding {
  /** Touche physique (e.code) : indépendante de la disposition de clavier. */
  code: string;
  ctrl: boolean;
  alt: boolean;
  shift: boolean;
  meta: boolean;
}

export type Bindings = Record<CommandId, ShortcutBinding>;

export const DEFAULT_BINDINGS: Bindings = {
  [COMMAND_IDS.NEW]: { code: 'KeyN', ctrl: true, alt: false, shift: false, meta: false },
  [COMMAND_IDS.SAVE]: { code: 'KeyS', ctrl: true, alt: false, shift: false, meta: false },
  [COMMAND_IDS.GLOBAL_SEARCH]: { code: 'KeyK', ctrl: true, alt: false, shift: false, meta: false },
  [COMMAND_IDS.HELP]: { code: 'F1', ctrl: false, alt: false, shift: false, meta: false },
  [COMMAND_IDS.ESC]: { code: 'Escape', ctrl: false, alt: false, shift: false, meta: false },
  [COMMAND_IDS.NAV_DASHBOARD]: { code: 'Digit1', ctrl: false, alt: true, shift: false, meta: false },
  [COMMAND_IDS.NAV_INVOICES]: { code: 'Digit2', ctrl: false, alt: true, shift: false, meta: false },
  [COMMAND_IDS.NAV_CLIENTS]: { code: 'Digit3', ctrl: false, alt: true, shift: false, meta: false },
  [COMMAND_IDS.NAV_PRODUCTS]: { code: 'Digit4', ctrl: false, alt: true, shift: false, meta: false },
  [COMMAND_IDS.NAV_REPORTS]: { code: 'Digit5', ctrl: false, alt: true, shift: false, meta: false },
  [COMMAND_IDS.NAV_SETTINGS]: { code: 'Digit6', ctrl: false, alt: true, shift: false, meta: false },
  [COMMAND_IDS.NAV_BACK]: { code: 'ArrowLeft', ctrl: false, alt: true, shift: false, meta: false },
  [COMMAND_IDS.NAV_FORWARD]: { code: 'ArrowRight', ctrl: false, alt: true, shift: false, meta: false },
  [COMMAND_IDS.FOCUS_SEARCH]: { code: 'KeyF', ctrl: true, alt: false, shift: false, meta: false },
  [COMMAND_IDS.FINALIZE]: { code: 'Enter', ctrl: true, alt: false, shift: false, meta: false },
  [COMMAND_IDS.PRINT]: { code: 'KeyP', ctrl: true, alt: false, shift: false, meta: false },
  [COMMAND_IDS.DUPLICATE]: { code: 'KeyD', ctrl: true, alt: false, shift: false, meta: false },
  [COMMAND_IDS.EXPORT]: { code: 'KeyE', ctrl: true, alt: false, shift: false, meta: false },
  [COMMAND_IDS.OPEN_SELECTED]: { code: 'Enter', ctrl: false, alt: false, shift: false, meta: false },
};

const STORAGE_KEY = 'mohasabi_shortcuts';

/** Représentation canonique d'une combinaison (comparaison d'unicité). */
export function shortcutKey(binding: ShortcutBinding): string {
  const mods = `${binding.ctrl ? 'Ctrl+' : ''}${binding.alt ? 'Alt+' : ''}${binding.shift ? 'Shift+' : ''}${binding.meta ? 'Meta+' : ''}`;
  return mods + binding.code;
}

/** Touches purement modificatrices : inacceptables comme raccourci seul. */
export function isModifierCode(code: string): boolean {
  return /^(Control|Shift|Alt|Meta)/.test(code);
}

export function isValidBinding(binding: ShortcutBinding | null | undefined): boolean {
  return Boolean(binding && binding.code && !isModifierCode(binding.code));
}

/** Renvoie l'id d'une autre commande utilisant la même combinaison, sinon null. */
export function findConflict(bindings: Bindings, id: CommandId, binding: ShortcutBinding): CommandId | null {
  const key = shortcutKey(binding);
  for (const other of Object.keys(bindings) as CommandId[]) {
    if (other === id) continue;
    if (shortcutKey(bindings[other]) === key) return other;
  }
  return null;
}

/** Libellé lisible d'une touche physique (ex. 'KeyN' → 'N'). */
export function formatKey(code: string): string {
  if (code.startsWith('Key')) return code.slice(3);
  if (code.startsWith('Digit')) return code.slice(5);
  if (code.startsWith('Numpad')) return 'NumPad ' + code.slice(6);
  const map: Record<string, string> = {
    Space: 'Espace',
    Enter: 'Entrée',
    Tab: 'Tab',
    Backspace: 'Retour arrière',
    Comma: ',',
    Period: '.',
    Semicolon: ';',
    Quote: "'",
    BracketLeft: '[',
    BracketRight: ']',
    Backslash: '\\',
    Slash: '/',
    Minus: '-',
    Equal: '=',
    ArrowUp: '↑',
    ArrowDown: '↓',
    ArrowLeft: '←',
    ArrowRight: '→',
  };
  return map[code] ?? code;
}

/** Libellé lisible complet (ex. 'Ctrl + N'). Les noms de modificateurs sont fixés. */
export function formatShortcut(binding: ShortcutBinding): string {
  const mods = [
    binding.ctrl ? 'Ctrl' : null,
    binding.alt ? 'Alt' : null,
    binding.shift ? 'Maj' : null,
    binding.meta ? 'Méta' : null,
  ].filter(Boolean) as string[];
  return [...mods, formatKey(binding.code)].join(' + ');
}

function load(): Bindings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return DEFAULT_BINDINGS;
    const parsed = JSON.parse(raw) as Partial<Bindings>;
    const bindings = { ...DEFAULT_BINDINGS };
    for (const id of Object.keys(DEFAULT_BINDINGS) as CommandId[]) {
      const b = parsed[id];
      if (b && typeof b.code === 'string') {
        bindings[id] = {
          code: b.code,
          ctrl: Boolean(b.ctrl),
          alt: Boolean(b.alt),
          shift: Boolean(b.shift),
          meta: Boolean(b.meta),
        };
      }
    }
    return bindings;
  } catch {
    return DEFAULT_BINDINGS;
  }
}

function persist(bindings: Bindings) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(bindings));
  } catch {
    // Stockage indisponible : les raccourcis restent valables pour la session.
  }
}

interface ShortcutsState {
  bindings: Bindings;
  /** Persiste et applique un jeu complet de raccourcis (utilisé au Save). */
  commit: (bindings: Bindings) => void;
  setBinding: (id: CommandId, binding: ShortcutBinding) => void;
  resetBinding: (id: CommandId) => void;
  resetAll: () => void;
}

export const useShortcutsStore = create<ShortcutsState>((set, get) => ({
  bindings: load(),

  commit: (bindings) => {
    persist(bindings);
    set({ bindings: { ...bindings } });
  },

  setBinding: (id, binding) => {
    const next = { ...get().bindings, [id]: binding };
    persist(next);
    set({ bindings: next });
  },

  resetBinding: (id) => {
    const next = { ...get().bindings, [id]: DEFAULT_BINDINGS[id] };
    persist(next);
    set({ bindings: next });
  },

  resetAll: () => {
    persist(DEFAULT_BINDINGS);
    set({ bindings: DEFAULT_BINDINGS });
  },
}));
