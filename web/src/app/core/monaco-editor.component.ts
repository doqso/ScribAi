import { AfterViewInit, Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild } from '@angular/core';

declare const monaco: any;

let monacoLoading: Promise<void> | null = null;

function loadMonaco(): Promise<void> {
  if (typeof (window as any).monaco !== 'undefined') return Promise.resolve();
  if (monacoLoading) return monacoLoading;

  monacoLoading = new Promise<void>((resolve, reject) => {
    const base = '/assets/monaco/vs';
    const loaderScript = document.createElement('script');
    loaderScript.src = base + '/loader.js';
    loaderScript.onload = () => {
      (window as any).require.config({ paths: { vs: base } });
      (window as any).require(['vs/editor/editor.main'], () => resolve(), reject);
    };
    loaderScript.onerror = reject;
    document.head.appendChild(loaderScript);
  });
  return monacoLoading;
}

@Component({
  selector: 'app-monaco',
  standalone: true,
  template: `<div class="editor-host" #host></div>`,
  styles: [`
    :host { display:block; }
    .editor-host { width:100%; height: var(--monaco-height, 300px); border:1px solid #333; border-radius:4px; overflow:hidden; }
  `]
})
export class MonacoComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('host', { static: true }) host!: ElementRef<HTMLDivElement>;
  @Input() value = '';
  @Input() language = 'json';
  @Input() readOnly = false;
  @Input() height = '300px';
  @Output() valueChange = new EventEmitter<string>();

  private editor: any;
  private subscribed = false;

  async ngAfterViewInit() {
    this.host.nativeElement.style.setProperty('--monaco-height', this.height);
    await loadMonaco();
    this.editor = monaco.editor.create(this.host.nativeElement, {
      value: this.value,
      language: this.language,
      theme: 'vs-dark',
      readOnly: this.readOnly,
      automaticLayout: true,
      minimap: { enabled: false },
      fontSize: 13,
      tabSize: 2,
      scrollBeyondLastLine: false,
      wordWrap: 'on'
    });

    if (!this.readOnly) {
      this.editor.onDidChangeModelContent(() => {
        this.valueChange.emit(this.editor.getValue());
      });
    }
    this.subscribed = true;
  }

  ngOnChanges(changes: SimpleChanges) {
    if (!this.editor) return;
    if (changes['value'] && this.editor.getValue() !== this.value) {
      this.editor.setValue(this.value ?? '');
    }
    if (changes['readOnly']) {
      this.editor.updateOptions({ readOnly: this.readOnly });
    }
    if (changes['language']) {
      monaco.editor.setModelLanguage(this.editor.getModel(), this.language);
    }
  }

  ngOnDestroy() {
    this.editor?.dispose();
  }
}
