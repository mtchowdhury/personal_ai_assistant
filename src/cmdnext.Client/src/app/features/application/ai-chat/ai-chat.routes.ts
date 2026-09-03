import { Routes } from '@angular/router';
import { AiChatComponent } from './components/ai-chat/ai-chat.component';

export const routes: Routes = [
  {
    path: '',
    component: AiChatComponent,
    data: { title: 'AI Chat' }
  }
];
