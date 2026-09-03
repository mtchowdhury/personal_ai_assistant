import { Component, ElementRef, Input, OnDestroy, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import lottie, { AnimationItem } from 'lottie-web';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './loading-spinner.component.html',
  styleUrls: ['./loading-spinner.component.scss']
})
export class LoadingSpinnerComponent implements AfterViewInit, OnDestroy {
  /** Optional caption shown under the animation, e.g. "Loading expenses…". */
  @Input() label?: string;
  @Input() size = 160;

  @ViewChild('animationHost', { static: true }) private animationHost!: ElementRef<HTMLDivElement>;

  private animation: AnimationItem | null = null;

  ngAfterViewInit(): void {
    this.animation = lottie.loadAnimation({
      container: this.animationHost.nativeElement,
      renderer: 'svg',
      loop: true,
      autoplay: true,
      path: 'assets/icons/loading.json'
    });
  }

  ngOnDestroy(): void {
    this.animation?.destroy();
  }
}
