import { create } from 'zustand';

export interface UpdateInfo {
  updateAvailable: boolean;
  latestVersion?: string;
  releaseNotes?: string;
}

interface UpdateState extends UpdateInfo {
  checked: boolean;
  setUpdate: (info: UpdateInfo) => void;
  reset: () => void;
}

export const useUpdateStore = create<UpdateState>((set) => ({
  updateAvailable: false,
  latestVersion: undefined,
  releaseNotes: undefined,
  checked: false,
  setUpdate: (info) => set({ ...info, checked: true }),
  reset: () => set({ updateAvailable: false, latestVersion: undefined, releaseNotes: undefined, checked: false }),
}));
