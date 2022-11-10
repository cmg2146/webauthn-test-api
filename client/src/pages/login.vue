<template>
  <v-container class="px-10 pt-10">
    <v-row justify="center" align="start" class="mb-5">
      <v-col cols="12" class="d-flex justify-center">
        <v-img
          :src="require('~/static/logo.svg')"
          alt="WebAuthn Logo"
          height="75"
          width="75"
          contain
        ></v-img>       
      </v-col>
    </v-row>
    <v-row justify="center" align="start">
      <v-col cols="12" class="d-flex justify-center">
        <h2>
          Login to WebAuthn-Test
        </h2>        
      </v-col>
    </v-row>
    <v-row justify="center" align="start">
      <v-col cols="12" class="d-flex justify-center">
        <v-card
          class="login-card pa-5"
        >
          <v-form
            @submit.prevent="onLoginSubmit"
          >
            <v-card-text>
              Click below to login with your computer, phone, or security key.
            </v-card-text>
            <v-card-text>
              <v-btn
                block
                color="primary"
                :loading="authenticating"
                type="submit"
              >Login</v-btn>
            </v-card-text>
          </v-form>
          <v-card-text>
            <span>Or </span>
            <NuxtLink
              to="/register"
            >create an account</NuxtLink>
          </v-card-text>          
        </v-card>
      </v-col>
    </v-row>
    <v-row justify="center" align="start">
      <v-col cols="12" md="6" lg="5" class="d-flex justify-center">
        <v-alert
          v-model="authenticationError"
          color="error"
          class="login-card pa-5"
        >
          {{authenticationErrorMessage}}  
        </v-alert>
      </v-col>
    </v-row>
  </v-container>
</template>

<script>
import { startAuthentication } from '@simplewebauthn/browser';

export default {
  name: 'LoginPage',
  auth: 'guest',
  data () {
    return {
      authenticating: false,
      authenticationError: false,
      authenticationErrorMessage: '',
    };
  },
  methods: {
    onLoginSubmit() {
      this.authenticating = true;
      this.authenticationError = false;
      this.authenticationErrorMessage = '';

      return this
        .$axios
        //retrieve authentication options/challenge first
        .$get('/api/webauthn/authenticate')
        //then start auth ceremony
        .then((authOptions) => startAuthentication(authOptions))
        //then try to log the user in with the authenticator response
        .then((assertionResponse) => this.$auth.loginWith('cookie', { data: assertionResponse }))
        .catch((error) => {
          this.authenticating = false;
          this.authenticationError = true;
          this.authenticationErrorMessage = error;
        });
    }
  }
};
</script>

<style scoped>
  .login-card {
    width: 400px;
  }
</style>