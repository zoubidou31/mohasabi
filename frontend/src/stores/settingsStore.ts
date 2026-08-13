import { create } from 'zustand';
import i18n from '../i18n';
import { api, extractError } from '../api/client';
import type { AppSettings, RestoreResult } from '../api/types';

interface SettingsState {
  settings: AppSettings | null;
  loaded: boolean;
  uncleanExit: boolean;
  load: () => Promise<void>;
  applyLocal: (settings: AppSettings) => void;
  save: (settings: AppSettings) => Promise<void>;
  restartApp: () => Promise<RestoreResult | undefined>;
}

const defaultSettings: AppSettings = {
  language: 'fr',
  theme: 'light',
  autoBackupEnabled: true,
  backupFrequencyMinutes: 30,
  backupRetentionCount: 5,
  backupLocation: '',
  splashEnabled: true,
  appFontFamily: 'Inter',
  interfaceFontSize: 'medium',
  docFontFamily: 'Inter',
  docBaseFontSize: 11,
  docTableFontSize: 9,
  docHeaderFontSize: 13,
  docFooterFontSize: 9,
};

export const useSettingsStore = create<SettingsState>((set) => ({
  settings: null,
  loaded: false,
  uncleanExit: false,

  load: async () => {
    try {
      const { data } = await api.get<AppSettings>('/settings');
      set({ settings: { ...defaultSettings, ...data }, loaded: true });
      applySettings(data);
    } catch {
      set({ settings: defaultSettings, loaded: true });
    }

    try {
      const { data } = await api.get<{ uncleanExit: boolean }>('/app/status');
      set({ uncleanExit: Boolean(data?.uncleanExit) });
    } catch {
      // Ignoré.
    }
  },

  applyLocal: (settings) => {
    set({ settings });
    applySettings(settings);
  },

  save: async (settings) => {
    const { data } = await api.put<AppSettings>('/settings', settings);
    const normalized = { ...defaultSettings, ...data };
    set({ settings: normalized });
    applySettings(normalized);
  },

  restartApp: async () => {
    try {
      const { data } = await api.post<RestoreResult>('/app/restart', {});
      return data;
    } catch (err) {
      return { success: false, requiresRestart: false, error: extractError(err) };
    }
  },
}));

function applySettings(settings: AppSettings) {
  if (settings.language) {
    const lang = settings.language === 'en' ? 'en' : 'fr';
    if (i18n.language !== lang) {
      localStorage.setItem('mohasabi_lang', lang);
      void i18n.changeLanguage(lang);
      document.documentElement.lang = lang;
    }
  }

  if (settings.theme) {
    localStorage.setItem('mohasabi_theme', settings.theme);
  }

  const resolvedTheme = resolveTheme(settings.theme);
  if (resolvedTheme === 'dark') {
    document.documentElement.classList.add('dark');
  } else {
    document.documentElement.classList.remove('dark');
  }

  // Interface typography (program UI only — never affects exports).
  const fontFamily = settings.appFontFamily || 'Inter';
  const fontSize =
    settings.interfaceFontSize === 'small' ? 13 : settings.interfaceFontSize === 'large' ? 16 : 14;
  document.documentElement.style.setProperty('--moha-app-font', `'${fontFamily}', sans-serif`);
  document.documentElement.style.setProperty('--moha-app-font-size', `${fontSize}px`);
}

export function resolveTheme(preference: string): 'light' | 'dark' {
  if (preference === 'dark') return 'dark';
  if (preference === 'system') {
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  return 'light';
}
