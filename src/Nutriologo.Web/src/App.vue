<template>
  <nav class="app-nav">
    <RouterLink to="/">Inicio</RouterLink>
    <RouterLink to="/perfil">Perfil</RouterLink>
    <RouterLink to="/mediciones">Mediciones</RouterLink>
    <RouterLink to="/objetivo">Objetivo</RouterLink>
    <RouterLink to="/registrar">Registrar</RouterLink>
    <RouterLink to="/chat">Chat</RouterLink>
  </nav>
  <main class="app-main">
    <RouterView />
  </main>
</template>
