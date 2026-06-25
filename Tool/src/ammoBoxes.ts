import { AMMO_BOXES_PART1 } from './ammoBoxes_part1'
import { AMMO_BOXES_PART2 } from './ammoBoxes_part2'
import { AMMO_BOXES_PART3 } from './ammoBoxes_part3'

export interface AmmoBoxInfo {
  roundTpl: string
  count: number
  name: string
}

export const AMMO_BOX_PARENT = '543be5cb4bdc2deb348b4568'

export const AMMO_BOXES: Record<string, AmmoBoxInfo> = {
  ...AMMO_BOXES_PART1,
  ...AMMO_BOXES_PART2,
  ...AMMO_BOXES_PART3,
}

export function getAmmoBoxInfo(tpl: string): AmmoBoxInfo | undefined {
  return AMMO_BOXES[tpl]
}

export function isAmmoBox(tpl: string): boolean {
  return tpl in AMMO_BOXES
}
