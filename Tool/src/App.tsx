import { useState, useCallback, useEffect } from 'react'
import {
  Store, Plus, Trash2, Download, AlertCircle, CheckCircle,
  ChevronDown, ChevronUp, Copy, RefreshCw, Eye, Package,
  Shield, Star, Settings, FileJson, HelpCircle, ExternalLink, Upload, Crosshair,
} from 'lucide-react'
import JSZip from 'jszip'
import { saveAs } from 'file-saver'
import type {
  TraderDefinition, AssortItem, LoyaltyLevel, BarterRequirement, ValidationError,
  QuestPackDefinition,
} from './types'
import {
  createDefaultTrader, createDefaultAssortItem, createDefaultBarter, generateMongoId,
  createDefaultQuestPack,
} from './types'
import { validateTrader, buildExportJson, validateQuestPack, buildQuestExportJson } from './validation'
import QuestsTab from './QuestsTab'

// Item name lookup via db.sp-tarkov.com API (fetches all names once, then caches)
let itemNameDb: Map<string, string> | null = null
let itemNameDbLoading = false
let itemNameDbListeners: Array<() => void> = []

async function loadItemNameDb() {
  if (itemNameDb || itemNameDbLoading) return
  itemNameDbLoading = true
  try {
    const res = await fetch('https://db.sp-tarkov.com/api/item/names')
    const data = await res.json()
    itemNameDb = new Map()
    for (const entry of data) {
      const id = entry?.item?._id
      const name = entry?.locale?.Name || entry?.locale?.ShortName
      if (id && name) itemNameDb.set(id, name)
    }
  } catch {
    itemNameDb = new Map()
  }
  itemNameDbLoading = false
  itemNameDbListeners.forEach(fn => fn())
  itemNameDbListeners = []
}

function useItemNames(itemIds: string[]) {
  const [names, setNames] = useState<Map<string, string>>(new Map())

  useEffect(() => {
    if (itemIds.length === 0) return

    const resolve = () => {
      if (!itemNameDb) return
      const result = new Map<string, string>()
      itemIds.forEach(id => {
        const name = itemNameDb!.get(id)
        if (name) result.set(id, name)
      })
      setNames(result)
    }

    if (itemNameDb) {
      resolve()
    } else {
      itemNameDbListeners.push(resolve)
      loadItemNameDb()
    }
  }, [itemIds.join(',')])

  return names
}

type Tab = 'general' | 'loyalty' | 'assort' | 'quests' | 'preview'

export default function App() {
  const [trader, setTrader] = useState<TraderDefinition>(createDefaultTrader)
  const [questPack, setQuestPack] = useState<QuestPackDefinition>(createDefaultQuestPack)
  const [errors, setErrors] = useState<ValidationError[]>([])
  const [questErrors, setQuestErrors] = useState<ValidationError[]>([])
  const [activeTab, setActiveTab] = useState<Tab>('general')
  const [expandedAssort, setExpandedAssort] = useState<Set<number>>(new Set())
  const [showExportSuccess, setShowExportSuccess] = useState(false)

  const update = useCallback(<K extends keyof TraderDefinition>(key: K, value: TraderDefinition[K]) => {
    setTrader(prev => ({ ...prev, [key]: value }))
    setErrors([])
  }, [])

  const validate = useCallback(() => {
    const traderErrs = validateTrader(trader)
    setErrors(traderErrs)
    const hasQuests = questPack.storyQuests.length > 0 || questPack.rotatingQuests.length > 0
    const qErrs = hasQuests ? validateQuestPack(questPack, trader.id) : []
    setQuestErrors(qErrs)
    return traderErrs.length === 0 && qErrs.length === 0
  }, [trader, questPack])

  const handleExport = useCallback(async () => {
    if (!validate()) {
      setActiveTab('general')
      return
    }
    const json = buildExportJson(trader)
    const jsonStr = JSON.stringify(json, null, 2)
    const packName = trader.packName.trim() || 'MyTraderPack'
    const basePath = `SPT/user/mods/TraderGen/traders/${packName}`

    const zip = new JSZip()
    zip.file(`${basePath}/trader.json`, jsonStr)

    if (trader.avatarDataUrl) {
      const match = trader.avatarDataUrl.match(/^data:image\/\w+;base64,(.+)$/)
      if (match) {
        zip.file(`${basePath}/assets/avatar.jpg`, match[1], { base64: true })
      }
    }

    // Quest pack (only if quests exist)
    const questJson = buildQuestExportJson(questPack)
    if (questJson) {
      zip.file(`${basePath}/quests.json`, JSON.stringify(questJson, null, 2))
    }

    // Quest icons
    if (questPack.defaultQuestIconDataUrl) {
      const match = questPack.defaultQuestIconDataUrl.match(/^data:image\/\w+;base64,(.+)$/)
      if (match) {
        zip.file(`${basePath}/assets/default_quest_icon.png`, match[1], { base64: true })
      }
    }
    for (const q of questPack.storyQuests) {
      if (q.imageDataUrl) {
        const match = q.imageDataUrl.match(/^data:image\/\w+;base64,(.+)$/)
        if (match) {
          zip.file(`${basePath}/assets/quest_${q.id}.png`, match[1], { base64: true })
        }
      }
    }

    const blob = await zip.generateAsync({ type: 'blob' })
    saveAs(blob, `${packName}.zip`)
    setShowExportSuccess(true)
    setTimeout(() => setShowExportSuccess(false), 3000)
  }, [trader, validate])

  const importFromJson = useCallback((jsonStr: string, packName?: string, avatarDataUrl?: string, questJsonStr?: string, questIconDataUrl?: string, perQuestIconDataUrls?: Map<string, string>) => {
    try {
      const raw = jsonStr
        .replace(/\/\/.*$/gm, '')   // strip single-line comments
        .replace(/\/\*[\s\S]*?\*\//g, '') // strip block comments
        .replace(/,\s*([\]}])/g, '$1') // strip trailing commas
      const parsed = JSON.parse(raw)
      const merged = { ...createDefaultTrader(), ...parsed }
      if (packName) merged.packName = packName
      if (avatarDataUrl) merged.avatarDataUrl = avatarDataUrl
      if (merged.assort) {
        merged.assort = merged.assort.map((a: AssortItem) => ({
          ...createDefaultAssortItem(),
          ...a,
        }))
      }
      setTrader(merged)
      setErrors([])

      // Import quest pack if present
      if (questJsonStr) {
        try {
          const questRaw = questJsonStr
            .replace(/\/\/.*$/gm, '')
            .replace(/\/\*[\s\S]*?\*\//g, '')
            .replace(/,\s*([\]}])/g, '$1')
          const questParsed = JSON.parse(questRaw)
          const pack: QuestPackDefinition = {
            ...createDefaultQuestPack(),
            ...questParsed,
          }
          if (questIconDataUrl) pack.defaultQuestIconDataUrl = questIconDataUrl
          // Match per-quest icons by quest ID
          if (perQuestIconDataUrls && perQuestIconDataUrls.size > 0 && pack.storyQuests) {
            pack.storyQuests = pack.storyQuests.map(q => {
              const iconUrl = perQuestIconDataUrls.get(q.id)
              if (iconUrl) {
                return { ...q, imageDataUrl: iconUrl }
              }
              return q
            })
          }
          setQuestPack(pack)
        } catch {
          console.warn('Failed to parse quests.json from import')
        }
      } else {
        setQuestPack(createDefaultQuestPack())
      }
      setQuestErrors([])
    } catch {
      alert('Failed to parse JSON file. Check the file format.')
    }
  }, [])

  const handleImport = useCallback(() => {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = '.json,.zip'
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0]
      if (!file) return

      if (file.name.endsWith('.zip')) {
        try {
          const zip = await JSZip.loadAsync(file)
          let jsonFile: JSZip.JSZipObject | null = null
          let avatarFile: JSZip.JSZipObject | null = null
          let questFile: JSZip.JSZipObject | null = null
          let questIconFile: JSZip.JSZipObject | null = null
          const perQuestIcons: Map<string, JSZip.JSZipObject> = new Map()
          let packName = ''

          for (const [path, entry] of Object.entries(zip.files)) {
            if (entry.dir) continue
            if (path.endsWith('trader.json')) {
              jsonFile = entry
              const parts = path.split('/')
              const traderIdx = parts.indexOf('traders')
              if (traderIdx >= 0 && parts[traderIdx + 1]) {
                packName = parts[traderIdx + 1]
              } else if (parts.length >= 2) {
                packName = parts[parts.length - 2]
              }
            }
            if (path.match(/assets\/avatar\.(jpg|jpeg|png|webp)$/i)) {
              avatarFile = entry
            }
            if (path.endsWith('quests.json')) {
              questFile = entry
            }
            if (path.match(/assets\/default_quest_icon\.png$/i)) {
              questIconFile = entry
            }
            // Match per-quest icons: assets/quest_{24-char-hex}.png
            const questIconMatch = path.match(/assets\/quest_([0-9a-fA-F]{24})\.png$/)
            if (questIconMatch) {
              perQuestIcons.set(questIconMatch[1], entry)
            }
          }

          if (!jsonFile) {
            alert('No trader.json found in ZIP.')
            return
          }

          const jsonStr = await jsonFile.async('string')
          let avatarDataUrl: string | undefined
          if (avatarFile) {
            const base64 = await avatarFile.async('base64')
            const ext = avatarFile.name.split('.').pop()?.toLowerCase() || 'jpg'
            const mime = ext === 'png' ? 'image/png' : ext === 'webp' ? 'image/webp' : 'image/jpeg'
            avatarDataUrl = `data:${mime};base64,${base64}`
          }

          const questJsonStr = questFile ? await questFile.async('string') : undefined
          let questIconDataUrl: string | undefined
          if (questIconFile) {
            const base64 = await questIconFile.async('base64')
            questIconDataUrl = `data:image/png;base64,${base64}`
          }

          // Resolve per-quest icon data URLs
          const perQuestIconDataUrls: Map<string, string> = new Map()
          for (const [questId, entry] of perQuestIcons) {
            const base64 = await entry.async('base64')
            perQuestIconDataUrls.set(questId, `data:image/png;base64,${base64}`)
          }

          importFromJson(jsonStr, packName || undefined, avatarDataUrl, questJsonStr, questIconDataUrl, perQuestIconDataUrls)
        } catch {
          alert('Failed to read ZIP file.')
        }
      } else {
        const reader = new FileReader()
        reader.onload = (ev) => {
          importFromJson(ev.target?.result as string)
        }
        reader.readAsText(file)
      }
    }
    input.click()
  }, [importFromJson])

  const addLoyaltyLevel = useCallback(() => {
    setTrader(prev => {
      const maxLevel = prev.loyaltyLevels.reduce((m, l) => Math.max(m, l.level), 0)
      const newLl: LoyaltyLevel = {
        level: maxLevel + 1,
        minLevel: maxLevel * 10 + 1,
        minSalesSum: maxLevel * 500000,
        minStanding: 0,
        buyPriceCoef: 40 + maxLevel * 5,
      }
      return { ...prev, loyaltyLevels: [...prev.loyaltyLevels, newLl] }
    })
  }, [])

  const removeLoyaltyLevel = useCallback((index: number) => {
    setTrader(prev => ({
      ...prev,
      loyaltyLevels: prev.loyaltyLevels.filter((_, i) => i !== index),
    }))
  }, [])

  const updateLoyaltyLevel = useCallback((index: number, key: keyof LoyaltyLevel, value: number) => {
    setTrader(prev => ({
      ...prev,
      loyaltyLevels: prev.loyaltyLevels.map((ll, i) => i === index ? { ...ll, [key]: value } : ll),
    }))
  }, [])

  const addAssortItem = useCallback(() => {
    setTrader(prev => ({ ...prev, assort: [...prev.assort, createDefaultAssortItem()] }))
    setExpandedAssort(prev => new Set([...prev, trader.assort.length]))
  }, [trader.assort.length])

  const removeAssortItem = useCallback((index: number) => {
    setTrader(prev => ({ ...prev, assort: prev.assort.filter((_, i) => i !== index) }))
    setExpandedAssort(prev => { const n = new Set(prev); n.delete(index); return n })
  }, [])

  const updateAssortItem = useCallback((index: number, key: keyof AssortItem, value: unknown) => {
    setTrader(prev => ({
      ...prev,
      assort: prev.assort.map((item, i) => i === index ? { ...item, [key]: value } : item),
    }))
  }, [])

  const toggleAssort = useCallback((index: number) => {
    setExpandedAssort(prev => {
      const n = new Set(prev)
      n.has(index) ? n.delete(index) : n.add(index)
      return n
    })
  }, [])

  const addBarter = useCallback((assortIndex: number) => {
    setTrader(prev => ({
      ...prev,
      assort: prev.assort.map((item, i) =>
        i === assortIndex
          ? { ...item, barter: [...(item.barter || []), createDefaultBarter()] }
          : item
      ),
    }))
  }, [])

  const removeBarter = useCallback((assortIndex: number, barterIndex: number) => {
    setTrader(prev => ({
      ...prev,
      assort: prev.assort.map((item, i) =>
        i === assortIndex
          ? { ...item, barter: (item.barter || []).filter((_, j) => j !== barterIndex) }
          : item
      ),
    }))
  }, [])

  const updateBarter = useCallback((assortIndex: number, barterIndex: number, key: keyof BarterRequirement, value: unknown) => {
    setTrader(prev => ({
      ...prev,
      assort: prev.assort.map((item, i) =>
        i === assortIndex
          ? {
              ...item,
              barter: (item.barter || []).map((b, j) =>
                j === barterIndex ? { ...b, [key]: value } : b
              ),
            }
          : item
      ),
    }))
  }, [])

  const errorsByField = (field: string) => errors.filter(e => e.field === field)
  const hasError = (field: string) => errors.some(e => e.field === field)

  const tabs: { id: Tab; label: string; icon: React.ReactNode }[] = [
    { id: 'general', label: 'General', icon: <Settings size={16} /> },
    { id: 'loyalty', label: 'Loyalty Levels', icon: <Star size={16} /> },
    { id: 'assort', label: 'Assortment', icon: <Package size={16} /> },
    { id: 'quests', label: 'Quests', icon: <Crosshair size={16} /> },
    { id: 'preview', label: 'JSON Preview', icon: <FileJson size={16} /> },
  ]

  return (
    <div className="min-h-screen flex flex-col">
      {/* Header */}
      <header className="bg-tarkov-surface border-b border-tarkov-border px-6 py-4 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Store className="text-tarkov-accent" size={28} />
          <div>
            <h1 className="text-xl font-bold text-tarkov-accent">TraderGen Tool</h1>
            <p className="text-xs text-tarkov-text-dim">SPTarkov 4.0.13 Trader Pack Editor</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <div className="relative group">
            <button onClick={handleImport} className="btn-secondary text-sm flex items-center gap-1.5">
              <RefreshCw size={14} /> Import (.json / .zip)
            </button>
            <div className="absolute right-0 top-full mt-2 w-64 bg-tarkov-surface border border-tarkov-border rounded-lg p-3 text-xs text-tarkov-text-dim shadow-xl opacity-0 pointer-events-none group-hover:opacity-100 group-hover:pointer-events-auto transition-opacity z-50">
              <p className="font-medium text-tarkov-text mb-1">Import a trader pack</p>
              <p><span className="text-tarkov-accent">.zip</span> — Loads trader data, pack name, and avatar image.</p>
              <p className="mt-1"><span className="text-tarkov-accent">.json</span> — Loads trader data only. Pack name and avatar must be set manually.</p>
            </div>
          </div>
          <button onClick={() => { setTrader(createDefaultTrader()); setQuestPack(createDefaultQuestPack()); setErrors([]); setQuestErrors([]) }}
            className="btn-secondary text-sm flex items-center gap-1.5">
            <Plus size={14} /> New Trader
          </button>
          <button onClick={handleExport} className="btn-primary text-sm flex items-center gap-1.5">
            <Download size={14} /> Export
          </button>
        </div>
      </header>

      {/* Success toast */}
      {showExportSuccess && (
        <div className="fixed top-4 right-4 z-50 bg-tarkov-success/20 border border-tarkov-success/50 text-tarkov-success px-4 py-3 rounded-lg flex items-center gap-2 shadow-lg">
          <CheckCircle size={18} /> Trader pack exported as ZIP!
        </div>
      )}

      {/* Errors banner */}
      {errors.length > 0 && (
        <div className="bg-tarkov-error/10 border-b border-tarkov-error/30 px-6 py-3">
          <div className="flex items-center gap-2 text-tarkov-error font-medium mb-1">
            <AlertCircle size={16} /> {errors.length} validation error(s) found
          </div>
          <ul className="text-sm text-tarkov-error/80 list-disc list-inside max-h-32 overflow-y-auto">
            {errors.map((e, i) => <li key={i}>{e.message}</li>)}
          </ul>
        </div>
      )}

      {/* Tabs */}
      <nav className="bg-tarkov-surface border-b border-tarkov-border px-6 flex gap-1">
        {tabs.map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`flex items-center gap-1.5 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === tab.id
                ? 'border-tarkov-accent text-tarkov-accent'
                : 'border-transparent text-tarkov-text-dim hover:text-tarkov-text'
            }`}
          >
            {tab.icon} {tab.label}
            {tab.id === 'assort' && trader.assort.length > 0 && (
              <span className="ml-1 bg-tarkov-accent/20 text-tarkov-accent text-xs px-1.5 py-0.5 rounded-full">
                {trader.assort.length}
              </span>
            )}
            {tab.id === 'quests' && (questPack.storyQuests.length > 0 || questPack.rotatingQuests.length > 0) && (
              <span className="ml-1 bg-tarkov-accent/20 text-tarkov-accent text-xs px-1.5 py-0.5 rounded-full">
                {questPack.storyQuests.length + questPack.rotatingQuests.length}
              </span>
            )}
          </button>
        ))}
      </nav>

      {/* Content */}
      <main className="flex-1 p-6 max-w-5xl mx-auto w-full">
        {activeTab === 'general' && (
          <GeneralTab trader={trader} update={update} hasError={hasError} errorsByField={errorsByField} />
        )}
        {activeTab === 'loyalty' && (
          <LoyaltyTab
            levels={trader.loyaltyLevels}
            onAdd={addLoyaltyLevel}
            onRemove={removeLoyaltyLevel}
            onUpdate={updateLoyaltyLevel}
          />
        )}
        {activeTab === 'quests' && (
          <QuestsTab
            questPack={questPack}
            traderId={trader.id}
            onChange={pack => { setQuestPack(pack); setQuestErrors([]) }}
            errors={questErrors}
          />
        )}
        {activeTab === 'assort' && (
          <AssortTab
            assort={trader.assort}
            loyaltyLevels={trader.loyaltyLevels}
            defaultCurrency={trader.currency}
            expanded={expandedAssort}
            onToggle={toggleAssort}
            onAdd={addAssortItem}
            onRemove={removeAssortItem}
            onUpdate={updateAssortItem}
            onAddBarter={addBarter}
            onRemoveBarter={removeBarter}
            onUpdateBarter={updateBarter}
            errors={errors}
          />
        )}
        {activeTab === 'preview' && (
          <PreviewTab trader={trader} questPack={questPack} onValidate={validate} />
        )}
      </main>
    </div>
  )
}

/* ===== GENERAL TAB ===== */
function GeneralTab({ trader, update, hasError, errorsByField }: {
  trader: TraderDefinition
  update: <K extends keyof TraderDefinition>(key: K, value: TraderDefinition[K]) => void
  hasError: (f: string) => boolean
  errorsByField: (f: string) => ValidationError[]
}) {
  return (
    <div className="space-y-6">
      <section className="card">
        <h2 className="text-lg font-semibold text-tarkov-accent mb-4 flex items-center gap-2">
          <Shield size={18} /> Identity
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Field label="Trader ID" error={hasError('id')} tooltip="A unique 24-character hexadecimal string that identifies this trader. Click the refresh button to generate one automatically.">
            <div className="flex gap-2">
              <input className="input-field flex-1 font-mono text-sm" value={trader.id}
                onChange={e => update('id', e.target.value)} placeholder="24-char hex string" maxLength={24} />
              <button onClick={() => update('id', generateMongoId())} className="btn-secondary text-xs px-2"
                title="Generate random ID"><RefreshCw size={14} /></button>
            </div>
            <FieldErrors errors={errorsByField('id')} />
          </Field>

          <Field label="Nickname (Display Name)" error={hasError('nickname')} tooltip="The name shown in the trader list in-game. This is the primary name players will see.">
            <input className="input-field" value={trader.nickname}
              onChange={e => update('nickname', e.target.value)} placeholder="e.g. Viktor" />
            <FieldErrors errors={errorsByField('nickname')} />
          </Field>

          <Field label="First Name" error={hasError('firstName')} tooltip="The trader's first name, used in locale/dialogue text.">
            <input className="input-field" value={trader.firstName}
              onChange={e => update('firstName', e.target.value)} placeholder="e.g. Viktor" />
            <FieldErrors errors={errorsByField('firstName')} />
          </Field>

          <Field label="Last Name" tooltip="The trader's surname. Defaults to 'Unknown' if left empty.">
            <input className="input-field" value={trader.lastName}
              onChange={e => update('lastName', e.target.value)} placeholder="e.g. Kozlov" />
          </Field>

          <Field label="Full Name (optional)" tooltip="Override for the full display name. If left empty, it will be set to 'Nickname LastName' automatically.">
            <input className="input-field" value={trader.fullName || ''}
              onChange={e => update('fullName', e.target.value || undefined as unknown as string)}
              placeholder="Defaults to Nickname + Last Name" />
          </Field>

          <Field label="Location" tooltip="The location text shown on the trader's screen in-game (e.g. 'Customs', 'Interchange Mall').">
            <input className="input-field" value={trader.location}
              onChange={e => update('location', e.target.value)} placeholder="e.g. Customs Warehouse" />
          </Field>
        </div>

        <div className="mt-4">
          <Field label="Description" tooltip="A backstory or description shown when clicking the trader's info button in-game.">
            <textarea className="input-field min-h-[80px] resize-y" value={trader.description}
              onChange={e => update('description', e.target.value)}
              placeholder="A short backstory or description of the trader..." />
          </Field>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
          <Field label="Pack Name" tooltip="The name of the trader pack folder. This becomes the folder name inside TraderGen/traders/ and the ZIP filename on export.">
            <input className="input-field" value={trader.packName}
              onChange={e => update('packName', e.target.value.replace(/[^a-zA-Z0-9_-]/g, ''))}
              placeholder="MyTraderPack" />
            <p className="text-xs text-tarkov-text-dim mt-1">
              Letters, numbers, dashes and underscores only
            </p>
          </Field>

          <Field label="Trader Avatar" tooltip="Drag and drop or click to upload the trader portrait. Should be a square image (332x332 recommended).">
            <div
              className={`relative border-2 border-dashed rounded-lg p-4 text-center cursor-pointer transition-colors
                ${trader.avatarDataUrl ? 'border-tarkov-accent/50 bg-tarkov-accent/5' : 'border-tarkov-border hover:border-tarkov-accent/40'}`}
              onDragOver={e => { e.preventDefault(); e.stopPropagation() }}
              onDrop={e => {
                e.preventDefault(); e.stopPropagation()
                const file = e.dataTransfer.files?.[0]
                if (file?.type.startsWith('image/')) {
                  const reader = new FileReader()
                  reader.onload = ev => update('avatarDataUrl', ev.target?.result as string)
                  reader.readAsDataURL(file)
                }
              }}
              onClick={() => {
                const input = document.createElement('input')
                input.type = 'file'
                input.accept = 'image/*'
                input.onchange = e => {
                  const file = (e.target as HTMLInputElement).files?.[0]
                  if (file) {
                    const reader = new FileReader()
                    reader.onload = ev => update('avatarDataUrl', ev.target?.result as string)
                    reader.readAsDataURL(file)
                  }
                }
                input.click()
              }}
            >
              {trader.avatarDataUrl ? (
                <div className="flex items-center gap-4">
                  <img src={trader.avatarDataUrl} alt="Avatar preview"
                    className="w-16 h-16 rounded object-cover border border-tarkov-border" />
                  <div className="text-left">
                    <p className="text-sm text-tarkov-text">Image loaded</p>
                    <p className="text-xs text-tarkov-text-dim">Click or drag to replace</p>
                  </div>
                </div>
              ) : (
                <div className="py-2">
                  <Upload size={24} className="mx-auto text-tarkov-text-dim mb-2" />
                  <p className="text-sm text-tarkov-text-dim">Drag & drop an image or click to browse</p>
                  <p className="text-xs text-tarkov-text-dim mt-1">Recommended: 332×332 px</p>
                </div>
              )}
            </div>
          </Field>
        </div>
      </section>

      <section className="card">
        <h2 className="text-lg font-semibold text-tarkov-accent mb-4 flex items-center gap-2">
          <Settings size={18} /> Settings
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <Field label="Default Currency" error={hasError('currency')} tooltip="The default currency this trader uses when selling items. Individual items can override this.">
            <select className="input-field" value={trader.currency}
              onChange={e => update('currency', e.target.value)}>
              <option value="RUB">Roubles (RUB)</option>
              <option value="USD">Dollars (USD)</option>
              <option value="EUR">Euros (EUR)</option>
            </select>
          </Field>

          <Field label="Balance (Roubles)" tooltip="How many roubles the trader has available. Affects how much they can pay when buying items from the player.">
            <input type="number" className="input-field" value={trader.balanceRub}
              onChange={e => update('balanceRub', Number(e.target.value))} min={0} />
          </Field>

          <Field label="Balance (Dollars)" tooltip="How many dollars the trader has available for buying items from the player.">
            <input type="number" className="input-field" value={trader.balanceDol}
              onChange={e => update('balanceDol', Number(e.target.value))} min={0} />
          </Field>
        </div>

        <div className="flex flex-wrap justify-center gap-4 mt-4">
          <Toggle label="Enabled" value={trader.enabled}
            onChange={v => update('enabled', v)}
            tooltip="Master toggle. If off, TraderGen will skip this trader entirely without needing to delete the file." />
          <Toggle label="Unlocked by Default" value={trader.unlockedByDefault}
            onChange={v => update('unlockedByDefault', v)}
            tooltip="If on, the trader is available from level 1. If off, players must meet requirements to unlock." />
          <Toggle label="Buyer Enabled" value={trader.buyerEnabled}
            onChange={v => update('buyerEnabled', v)}
            tooltip="Whether this trader will buy items from the player." />
          <Toggle label="Ragfair Enabled" value={trader.ragfairEnabled}
            onChange={v => update('ragfairEnabled', v)}
            tooltip="Whether this trader's items appear on the Flea Market." />
          <Toggle label="Insurance" value={trader.insuranceEnabled}
            onChange={v => update('insuranceEnabled', v)}
            tooltip="Whether this trader offers item insurance. Most custom traders leave this off." />
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
          <Field label="Refresh Time Min (seconds)" tooltip="Minimum time in seconds before the trader restocks their items. Default: 1800 (30 min).">
            <input type="number" className="input-field" value={trader.refreshTimeMin}
              onChange={e => update('refreshTimeMin', Number(e.target.value))} min={60} />
          </Field>
          <Field label="Refresh Time Max (seconds)" tooltip="Maximum time in seconds before the trader restocks. The actual time is random between min and max. Default: 7200 (2 hr).">
            <input type="number" className="input-field" value={trader.refreshTimeMax}
              onChange={e => update('refreshTimeMax', Number(e.target.value))} min={60} />
          </Field>
        </div>
      </section>
    </div>
  )
}

/* ===== LOYALTY TAB ===== */
function LoyaltyTab({ levels, onAdd, onRemove, onUpdate }: {
  levels: LoyaltyLevel[]
  onAdd: () => void
  onRemove: (i: number) => void
  onUpdate: (i: number, key: keyof LoyaltyLevel, value: number) => void
}) {
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-tarkov-accent flex items-center gap-2">
          <Star size={18} /> Loyalty Levels ({levels.length})
        </h2>
        <button onClick={onAdd} className="btn-primary text-sm flex items-center gap-1.5">
          <Plus size={14} /> Add Level
        </button>
      </div>

      {levels.length === 0 && (
        <div className="card text-center text-tarkov-text-dim py-8">
          No loyalty levels defined. Add at least one.
        </div>
      )}

      <div className="grid gap-3">
        {levels.map((ll, i) => (
          <div key={i} className="card">
            <div className="flex items-center justify-between mb-3">
              <h3 className="font-semibold text-tarkov-accent">Level {ll.level}</h3>
              {levels.length > 1 && (
                <button onClick={() => onRemove(i)} className="text-tarkov-error hover:text-tarkov-error/80 transition-colors">
                  <Trash2 size={16} />
                </button>
              )}
            </div>
            <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
              <Field label="Level #" tooltip="The loyalty tier number (1, 2, 3, etc.). Items in the assortment reference this number.">
                <input type="number" className="input-field" value={ll.level}
                  onChange={e => onUpdate(i, 'level', Number(e.target.value))} min={1} max={10} />
              </Field>
              <Field label="Min Player Level" tooltip="The minimum player level required to unlock this loyalty tier.">
                <input type="number" className="input-field" value={ll.minLevel}
                  onChange={e => onUpdate(i, 'minLevel', Number(e.target.value))} min={1} />
              </Field>
              <Field label="Min Sales Sum" tooltip="Total amount of money the player must have spent with this trader to unlock this tier.">
                <input type="number" className="input-field" value={ll.minSalesSum}
                  onChange={e => onUpdate(i, 'minSalesSum', Number(e.target.value))} min={0} />
              </Field>
              <Field label="Min Standing" tooltip="Minimum reputation/standing the player needs with this trader. Usually 0 for custom traders.">
                <input type="number" className="input-field" value={ll.minStanding}
                  onChange={e => onUpdate(i, 'minStanding', Number(e.target.value))} step={0.01} />
              </Field>
              <Field label="Buy Price Coef" tooltip="Percentage coefficient for buy prices at this tier. Higher = trader pays more when buying from player. Typical range: 30-60.">
                <input type="number" className="input-field" value={ll.buyPriceCoef}
                  onChange={e => onUpdate(i, 'buyPriceCoef', Number(e.target.value))} min={0} max={100} />
              </Field>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

/* ===== ASSORT TAB ===== */
function AssortTab({ assort, loyaltyLevels, defaultCurrency, expanded, onToggle,
  onAdd, onRemove, onUpdate, onAddBarter, onRemoveBarter, onUpdateBarter, errors }: {
  assort: AssortItem[]
  loyaltyLevels: LoyaltyLevel[]
  defaultCurrency: string
  expanded: Set<number>
  onToggle: (i: number) => void
  onAdd: () => void
  onRemove: (i: number) => void
  onUpdate: (i: number, key: keyof AssortItem, value: unknown) => void
  onAddBarter: (i: number) => void
  onRemoveBarter: (ai: number, bi: number) => void
  onUpdateBarter: (ai: number, bi: number, key: keyof BarterRequirement, value: unknown) => void
  errors: ValidationError[]
}) {
  const itemIds = assort.map(a => a.itemTpl).filter(id => id.length === 24)
  const itemNames = useItemNames(itemIds)

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-tarkov-accent flex items-center gap-2">
          <Package size={18} /> Assortment ({assort.length} items)
        </h2>
        <div className="flex items-center gap-3">
          <a href="https://db.sp-tarkov.com/search" target="_blank" rel="noopener noreferrer"
            className="btn-secondary text-sm flex items-center gap-1.5">
            <ExternalLink size={14} /> SPT Item Database
          </a>
          <button onClick={onAdd} className="btn-primary text-sm flex items-center gap-1.5">
            <Plus size={14} /> Add Item
          </button>
        </div>
      </div>

      <div className="bg-tarkov-surface border border-tarkov-border rounded-lg px-4 py-2.5 text-sm text-tarkov-text-dim flex items-center gap-2">
        <HelpCircle size={14} className="text-tarkov-accent shrink-0" />
        Need item IDs? Search for items at{' '}
        <a href="https://db.sp-tarkov.com/search" target="_blank" rel="noopener noreferrer"
          className="text-tarkov-accent hover:text-tarkov-accent-hover underline">db.sp-tarkov.com/search</a>
        {' '}and copy the template ID.
      </div>

      {assort.length === 0 && (
        <div className="card text-center text-tarkov-text-dim py-8">
          No items in assortment. Add items the trader will sell.
        </div>
      )}

      <div className="space-y-2" id="assort-list-top">
        {assort.map((item, i) => {
          const isExpanded = expanded.has(i)
          const itemErrors = errors.filter(e => e.field.startsWith(`assort.${i}`))
          const isBarter = item.barter && item.barter.length > 0

          return (
            <div key={i} className={`card ${itemErrors.length > 0 ? 'border-tarkov-error/50' : ''}`}>
              {/* Collapsed header */}
              <div className="flex items-center justify-between cursor-pointer" onClick={() => onToggle(i)}>
                <div className="flex items-center gap-3">
                  {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                  <span className="font-mono text-sm text-tarkov-text-dim">
                    {item.itemTpl || '(no template ID)'}
                  </span>
                  <span className="text-xs bg-tarkov-accent/20 text-tarkov-accent px-2 py-0.5 rounded">
                    LL{item.loyaltyLevel}
                  </span>
                  {isBarter ? (
                    <span className="text-xs bg-purple-500/20 text-purple-400 px-2 py-0.5 rounded">Barter</span>
                  ) : (
                    <span className="text-xs text-tarkov-text-dim">
                      {item.price} {item.currency || defaultCurrency}
                    </span>
                  )}
                  {itemNames.get(item.itemTpl) && (
                    <span className="text-xs text-tarkov-text italic truncate max-w-[200px]">
                      {itemNames.get(item.itemTpl)}
                    </span>
                  )}
                  {itemErrors.length > 0 && (
                    <AlertCircle size={14} className="text-tarkov-error" />
                  )}
                </div>
                <button onClick={(e) => { e.stopPropagation(); onRemove(i) }}
                  className="text-tarkov-error hover:text-tarkov-error/80 transition-colors p-1">
                  <Trash2 size={14} />
                </button>
              </div>

              {/* Expanded content */}
              {isExpanded && (
                <div className="mt-4 pt-4 border-t border-tarkov-border space-y-4">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Field label="Item Template ID" tooltip="The 24-character hex ID of the item from the SPT database. Find IDs at db.sp-tarkov.com/search">
                      <input className="input-field font-mono text-sm" value={item.itemTpl}
                        onChange={e => onUpdate(i, 'itemTpl', e.target.value)}
                        placeholder="24-char hex ID from SPT database" maxLength={24} />
                      <p className="text-xs text-tarkov-text-dim mt-1">
                        Find IDs at{' '}
                        <a href="https://db.sp-tarkov.com/search" target="_blank" rel="noopener noreferrer"
                          className="text-tarkov-accent hover:text-tarkov-accent-hover underline">db.sp-tarkov.com</a>
                      </p>
                    </Field>

                    <Field label="Loyalty Level" tooltip="Which loyalty tier is required to see this item. Must match one of the levels defined in the Loyalty Levels tab.">
                      <select className="input-field" value={item.loyaltyLevel}
                        onChange={e => onUpdate(i, 'loyaltyLevel', Number(e.target.value))}>
                        {loyaltyLevels.map(ll => (
                          <option key={ll.level} value={ll.level}>Level {ll.level}</option>
                        ))}
                      </select>
                    </Field>
                  </div>

                  <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                    <Field label="Stock" tooltip="How many of this item the trader has in stock per restock cycle.">
                      <input type="number" className="input-field" value={item.stock}
                        onChange={e => onUpdate(i, 'stock', Number(e.target.value))} min={0} />
                    </Field>

                    <Toggle label="Unlimited Stock" value={item.unlimitedStock}
                      onChange={v => onUpdate(i, 'unlimitedStock', v)}
                      tooltip="If on, the trader never runs out of this item." />

                    <Field label="Buy Limit (0 = none)" tooltip="Maximum number of this item a player can buy per restock. Set to 0 for no limit.">
                      <input type="number" className="input-field" value={item.buyLimit}
                        onChange={e => onUpdate(i, 'buyLimit', Number(e.target.value))} min={0} />
                    </Field>
                  </div>

                  {/* Price OR Barter */}
                  <div className="bg-tarkov-bg rounded-lg p-4">
                    <div className="flex items-center justify-between mb-3">
                      <h4 className="text-sm font-semibold text-tarkov-text-dim">
                        {isBarter ? 'Barter Requirements' : 'Money Price'}
                      </h4>
                      <div className="flex gap-2">
                        {!isBarter && (
                          <button onClick={() => { onAddBarter(i); onUpdate(i, 'price', 0) }}
                            className="text-xs btn-secondary px-2 py-1">
                            Switch to Barter
                          </button>
                        )}
                        {isBarter && (
                          <button onClick={() => onUpdate(i, 'barter', undefined)}
                            className="text-xs btn-secondary px-2 py-1">
                            Switch to Money
                          </button>
                        )}
                      </div>
                    </div>

                    {!isBarter && (
                      <div className="grid grid-cols-2 gap-4">
                        <Field label="Price" tooltip="The cost of this item in the selected currency.">
                          <input type="number" className="input-field" value={item.price}
                            onChange={e => onUpdate(i, 'price', Number(e.target.value))} min={0} />
                        </Field>
                        <Field label="Currency (override)" tooltip="Override the trader's default currency for this specific item. Leave as default to use the trader's currency.">
                          <select className="input-field" value={item.currency || ''}
                            onChange={e => onUpdate(i, 'currency', e.target.value || undefined)}>
                            <option value="">Use trader default ({defaultCurrency})</option>
                            <option value="RUB">Roubles (RUB)</option>
                            <option value="USD">Dollars (USD)</option>
                            <option value="EUR">Euros (EUR)</option>
                          </select>
                        </Field>
                      </div>
                    )}

                    {isBarter && (
                      <div className="space-y-2">
                        {(item.barter || []).map((b, j) => (
                          <div key={j} className="flex items-end gap-2">
                            <div className="flex-1">
                              <label className="label">Item Template ID</label>
                              <input className="input-field font-mono text-sm" value={b.itemTpl}
                                onChange={e => onUpdateBarter(i, j, 'itemTpl', e.target.value)}
                                placeholder="24-char hex ID" maxLength={24} />
                            </div>
                            <div className="w-24">
                              <label className="label">Count</label>
                              <input type="number" className="input-field" value={b.count}
                                onChange={e => onUpdateBarter(i, j, 'count', Number(e.target.value))} min={1} />
                            </div>
                            <button onClick={() => onRemoveBarter(i, j)}
                              className="text-tarkov-error hover:text-tarkov-error/80 mb-2">
                              <Trash2 size={14} />
                            </button>
                          </div>
                        ))}
                        <button onClick={() => onAddBarter(i)} className="text-xs btn-secondary flex items-center gap-1 mt-2">
                          <Plus size={12} /> Add Barter Item
                        </button>
                      </div>
                    )}
                  </div>

                  {itemErrors.length > 0 && (
                    <div className="text-sm text-tarkov-error space-y-1">
                      {itemErrors.map((e, ei) => (
                        <div key={ei} className="flex items-center gap-1.5">
                          <AlertCircle size={12} /> {e.message}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          )
        })}
      </div>

      {assort.length > 3 && (
        <div className="flex items-center justify-center gap-4 pt-4">
          <button onClick={() => document.getElementById('assort-list-top')?.scrollIntoView({ behavior: 'smooth' })}
            className="btn-secondary text-sm flex items-center gap-1.5">
            <ChevronUp size={14} /> Back to Top
          </button>
          <button onClick={onAdd} className="btn-primary text-sm flex items-center gap-1.5">
            <Plus size={14} /> Add Item
          </button>
        </div>
      )}
    </div>
  )
}

/* ===== PREVIEW TAB ===== */
function PreviewTab({ trader, questPack, onValidate }: {
  trader: TraderDefinition
  questPack: QuestPackDefinition
  onValidate: () => boolean
}) {
  const [validateResult, setValidateResult] = useState<'pass' | 'fail' | null>(null)
  const [copied, setCopied] = useState(false)
  const [previewFile, setPreviewFile] = useState<'trader' | 'quests'>('trader')

  const traderJson = JSON.stringify(buildExportJson(trader), null, 2)
  const questJson = buildQuestExportJson(questPack)
  const questJsonStr = questJson ? JSON.stringify(questJson, null, 2) : null

  const activeJson = previewFile === 'trader' ? traderJson : (questJsonStr || '// No quests defined')

  const handleValidate = () => {
    const isValid = onValidate()
    setValidateResult(isValid ? 'pass' : 'fail')
    setTimeout(() => setValidateResult(null), 3000)
  }

  const copyToClipboard = () => {
    navigator.clipboard.writeText(activeJson)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-tarkov-accent flex items-center gap-2">
          <Eye size={18} /> JSON Preview
        </h2>
        <div className="flex gap-2">
          <button onClick={handleValidate} className={`text-sm flex items-center gap-1.5 ${
            validateResult === 'pass' ? 'btn-primary bg-tarkov-success border-tarkov-success' :
            validateResult === 'fail' ? 'btn-primary bg-tarkov-error border-tarkov-error' :
            'btn-secondary'
          }`}>
            {validateResult === 'pass' ? <><CheckCircle size={14} /> Valid!</> :
             validateResult === 'fail' ? <><AlertCircle size={14} /> Errors Found</> :
             <><CheckCircle size={14} /> Validate</>}
          </button>
          <button onClick={copyToClipboard} className={`text-sm flex items-center gap-1.5 ${
            copied ? 'btn-primary bg-tarkov-success border-tarkov-success' : 'btn-secondary'
          }`}>
            {copied ? <><CheckCircle size={14} /> Copied!</> : <><Copy size={14} /> Copy JSON</>}
          </button>
        </div>
      </div>

      {/* File toggle */}
      <div className="flex gap-1 bg-tarkov-surface border border-tarkov-border rounded-lg p-1">
        <button
          onClick={() => setPreviewFile('trader')}
          className={`flex-1 px-4 py-1.5 rounded text-sm font-medium transition-colors ${
            previewFile === 'trader' ? 'bg-tarkov-accent text-tarkov-bg' : 'text-tarkov-text-dim hover:text-tarkov-text'
          }`}
        >trader.json</button>
        <button
          onClick={() => setPreviewFile('quests')}
          className={`flex-1 px-4 py-1.5 rounded text-sm font-medium transition-colors ${
            previewFile === 'quests' ? 'bg-tarkov-accent text-tarkov-bg' : 'text-tarkov-text-dim hover:text-tarkov-text'
          }`}
        >
          quests.json
          {!questJsonStr && <span className="ml-1 text-xs opacity-60">(empty)</span>}
        </button>
      </div>

      {validateResult === 'pass' && (
        <div className="bg-tarkov-success/10 border border-tarkov-success/30 rounded-lg px-4 py-2.5 text-sm text-tarkov-success flex items-center gap-2">
          <CheckCircle size={16} /> JSON is valid and ready to export!
        </div>
      )}

      <div className="card">
        <pre className="text-sm font-mono text-tarkov-text overflow-x-auto max-h-[70vh] overflow-y-auto leading-relaxed whitespace-pre">
          {activeJson}
        </pre>
      </div>
    </div>
  )
}

/* ===== SHARED COMPONENTS ===== */
function Field({ label, error, tooltip, children }: {
  label: string
  error?: boolean
  tooltip?: string
  children: React.ReactNode
}) {
  return (
    <div>
      <label className={`label ${error ? 'text-tarkov-error' : ''} flex items-center gap-1.5`}>
        {label}
        {tooltip && (
          <span className="relative group">
            <HelpCircle size={13} className="text-tarkov-text-dim hover:text-tarkov-accent cursor-help transition-colors" />
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-2 bg-tarkov-bg border border-tarkov-border rounded-lg text-xs text-tarkov-text font-normal w-64 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-150 z-50 shadow-xl leading-relaxed pointer-events-none">
              {tooltip}
            </span>
          </span>
        )}
      </label>
      {children}
    </div>
  )
}

function FieldErrors({ errors }: { errors: ValidationError[] }) {
  if (errors.length === 0) return null
  return (
    <div className="mt-1 space-y-0.5">
      {errors.map((e, i) => (
        <p key={i} className="text-xs text-tarkov-error flex items-center gap-1">
          <AlertCircle size={10} /> {e.message}
        </p>
      ))}
    </div>
  )
}

function Toggle({ label, value, onChange, tooltip }: {
  label: string
  value: boolean
  onChange: (v: boolean) => void
  tooltip?: string
}) {
  return (
    <div className="flex items-center gap-2">
      <button
        onClick={() => onChange(!value)}
        className={`w-10 h-5 rounded-full transition-colors relative ${
          value ? 'bg-tarkov-accent' : 'bg-tarkov-border'
        }`}
      >
        <span className={`absolute top-0.5 w-4 h-4 rounded-full bg-white transition-transform ${
          value ? 'left-5' : 'left-0.5'
        }`} />
      </button>
      <span className="text-sm text-tarkov-text-dim flex items-center gap-1.5">
        {label}
        {tooltip && (
          <span className="relative group">
            <HelpCircle size={13} className="text-tarkov-text-dim hover:text-tarkov-accent cursor-help transition-colors" />
            <span className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-2 bg-tarkov-bg border border-tarkov-border rounded-lg text-xs text-tarkov-text font-normal w-64 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-150 z-50 shadow-xl leading-relaxed pointer-events-none">
              {tooltip}
            </span>
          </span>
        )}
      </span>
    </div>
  )
}
