import { getWilayas } from './algeriaLocations';

export interface Wilaya {
  code: string;
  name: string;
}

// Source unique : algeriaLocations.json (69 wilayas après la loi 26-06 2025/2026).
export const WILAYAS: Wilaya[] = getWilayas().map((w) => ({ code: w.code, name: w.nameFr }));

export const ALGERIAN_BANKS: string[] = [
  'BNA (Banque Nationale d\'Algérie)',
  'CPA (Crédit Populaire d\'Algérie)',
  'BADI (Banque de l\'Agriculture et du Développement Rural)',
  'BADR (Banque Algérienne du Développement Rural)',
  'BEA (Banque Extérieure d\'Algérie)',
  'BIAB (Banque Intercontinentale Algéro-Ouverture)',
  'BIS (Banque de l\'Industrie et du Commerce)',
  'BNP Paribas',
  'BMCE Bank',
  'Arab Bank',
  'Société Générale',
  'Trust Bank',
  'Crédit Foncier d\'Algérie',
];
