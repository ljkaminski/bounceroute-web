import type { InventoryCategory, InventoryItem, WaterUse } from '../api/client'

export const categoryLabels: Record<InventoryCategory, string> = {
  BounceHouse: 'Bounce houses',
  Combo: 'Combos',
  WaterSlide: 'Water slides',
  ObstacleCourse: 'Obstacle courses',
  SlipNSlide: 'Slip N Slides',
  Game: 'Games',
  Equipment: 'Equipment',
  Other: 'Other',
}

export const categoryOrder = Object.keys(categoryLabels) as InventoryCategory[]

export const waterUseLabels: Record<WaterUse, string> = {
  None: 'Not an inflatable',
  DryOnly: 'Dry only',
  WetOnly: 'Wet only',
  WetOrDry: 'Wet or dry',
}

export const sizeText = (item: Pick<InventoryItem, 'lengthFt' | 'widthFt' | 'heightFt'>) =>
  item.lengthFt && item.widthFt ? `${item.lengthFt}′ × ${item.widthFt}′${item.heightFt ? ` × ${item.heightFt}′ tall` : ''}` : ''

export const price = (amount: number | null) =>
  amount === null ? '' : amount.toLocaleString('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })

export function groupByCategory<T extends { category: InventoryCategory }>(items: T[]) {
  return categoryOrder
    .map((category) => ({ category, items: items.filter((item) => item.category === category) }))
    .filter((group) => group.items.length > 0)
}
