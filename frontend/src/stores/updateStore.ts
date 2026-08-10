import { create } from 'zustand';
import { api, extractError } from '../api/client';

export interface UpdateInfo {
  updateAvailable: boolean;
  latestVersion?: string;
  releaseNotes?: string;
  currentVersion?: string;
}

export interface UpdateInstallResult {
  message?: string;
  restarting?: boolean;
}

interface UpdateState extends UpdateInfo {
  checked: boolean;
  dialogOpen: boolean;
  dismissed: boolean;
  installing: boolean;
  installError: string;
  setUpdate: (info: UpdateInfo) => void;
  openDialog: () => void;
  dismissDialog: () => void;
  installNow: () => Promise<UpdateInstallResult | undefined>;
  reset: () => void;
}

export const useUpdateStore = create<UpdateState>((set, get) => ({
  updateAvailable: false,
  latestVersion: undefined,
  releaseNotes: undefined,
  currentVersion: undefined,
  checked: false,
  dialogOpen: false,
  dismissed: false,
  installing: false,
  installError: '',
  setUpdate: (info) => set({ ...info, checked: true }),
  openDialog: () => set({ dialogOpen: true }),
  dismissDialog: () => set({ dialogOpen: false, dismissed: true }),
  installNow: async () => {
    if (get().installing) return undefined;
    set({ installing: true, installError: '' });
    try {
      const { data } = await api.post<UpdateInstallResult>('/update/install', {});
      set({ dialogOpen: false });
      return data;
    } catch (err) {
      set({ installError: extractError(err) });
      return undefined;
    } finally {
      set({ installing: false });
    }
  },
  reset: () =>
    set({
      updateAvailable: false,
      latestVersion: undefined,
      releaseNotes: undefined,
      currentVersion: undefined,
      checked: false,
      dialogOpen: false,
      dismissed: false,
      installing: false,
      installError: '',
    }),
}));
