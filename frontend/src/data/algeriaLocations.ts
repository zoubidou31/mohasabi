import locationsData from './algeriaLocations.json';

export interface AlgeriaWilaya {
  code: string;
  nameFr: string;
  nameEn: string;
  postalPrefix: string | null;
}

export interface AlgeriaCommune {
  wilayaCode: string;
  nameFr: string;
  nameEn: string;
  dairaFr: string;
  postalCodes: string[];
}

export interface AlgeriaMetadata {
  schemaVersion: number;
  lastUpdated: string;
  postalCodeSource?: string;
  adminSource?: string;
}

interface RawLocationsData {
  wilayas: AlgeriaWilaya[];
  communes: AlgeriaCommune[];
  metadata?: AlgeriaMetadata;
}

const data = locationsData as unknown as RawLocationsData;

const communesByWilaya = new Map<string, AlgeriaCommune[]>();
for (const commune of data.communes) {
  const list = communesByWilaya.get(commune.wilayaCode) ?? [];
  list.push(commune);
  communesByWilaya.set(commune.wilayaCode, list);
}

export function getWilayas(): AlgeriaWilaya[] {
  return data.wilayas;
}

export function getWilaya(code?: string): AlgeriaWilaya | null {
  if (!code) return null;
  return data.wilayas.find((w) => w.code === code) ?? null;
}

export function getCommunes(wilayaCode?: string): AlgeriaCommune[] {
  if (!wilayaCode) return [];
  return communesByWilaya.get(wilayaCode) ?? [];
}

export function findCommune(wilayaCode?: string, communeName?: string): AlgeriaCommune | null {
  if (!wilayaCode || !communeName) return null;
  const name = communeName.trim();
  return (
    getCommunes(wilayaCode).find(
      (c) => c.nameFr.toLowerCase() === name.toLowerCase() || c.nameEn.toLowerCase() === name.toLowerCase(),
    ) ?? null
  );
}

export function getPostalCodes(wilayaCode?: string, communeName?: string): string[] {
  return findCommune(wilayaCode, communeName)?.postalCodes ?? [];
}

/**
 * Même règle que le backend (source unique algeriaLocations.json) :
 * - 5 chiffres ;
 * - wilaya connue : doit commencer par sa baraque officielle (pour 59-69,
 *   la baraque réelle d'Algérie Poste, pas le numéro de wilaya) ;
 * - wilaya + commune connues : doit être l'un des codes officiels de la commune ;
 *   les communes sans code officiel n'acceptent que la forme + la baraque.
 */
export function isValidPostalCode(wilayaCode?: string, communeName?: string, postalCode?: string): boolean {
  if (!postalCode) return true;
  const code = postalCode.trim();
  if (!/^\d{5}$/.test(code)) return false;

  const wilaya = getWilaya(wilayaCode);
  if (!wilaya) return true;

  const commune = findCommune(wilayaCode, communeName);
  if (!commune) return !wilaya.postalPrefix || code.startsWith(wilaya.postalPrefix);

  if (commune.postalCodes.length === 0) {
    return !wilaya.postalPrefix || code.startsWith(wilaya.postalPrefix);
  }

  return commune.postalCodes.includes(code);
}
