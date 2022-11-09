<template>
  <v-card
    class="pa-5"
  >
    <v-card-title>
      Devices Used to Login
    </v-card-title>

    <v-divider></v-divider>

    <v-card-text
      v-for="credential in credentials"
      :key="credential.id"
    >
      <template v-if="errorLoadingCredentials">
        <div class="py-15">There was an error loading your device list</div>
      </template>
      <template v-else-if="credentials.length === 0">
        <div class="py-15">
          You don't have any devices to log in with.
          Click below to add a device.
        </div>
      </template>
      <template v-else>
        <v-icon></v-icon>
        <span>{{ credential.displayName }}</span>

        <v-spacer></v-spacer>

        <v-menu
          bottom
          left
        >
          <template v-slot:activator="{ on, attrs }">
            <v-btn
              dark
              icon
              v-bind="attrs"
              v-on="on"
            >
              <v-icon>mdi-dots-vertical</v-icon>
            </v-btn>
          </template>

          <v-list>
            <v-list-item @click="onDeleteCredential(credential)">
              <v-list-item-icon color="error">
                <v-icon>mdi-logout</v-icon>
              </v-list-item-icon>
              <v-list-item-title>
                Delete
              </v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>
      </template>

      <v-fade-transition>
        <v-overlay
          absolute
          :value="loadingCredentials"
        >
        </v-overlay>
      </v-fade-transition>          
    </v-card-text>

    <v-divider></v-divider>

    <v-card-actions>
      <v-btn
        :loading="registering"
        @click="onAddDevice"
      >
        <v-icon>mdi-plus</v-icon>
        Add device
      </v-btn>     
    </v-card-actions>

    <v-snackbar
      :value="registrationError"
      color="error"
      bottom
    >
      Error adding device: {{registrationErrorMessage}}
    </v-snackbar>

    <v-snackbar
      :value="errorDeletingCredential"
      color="error"
      bottom
    >
      Error deleting device: {{credentialDeletionErrorMessage}}
    </v-snackbar>         
  </v-card>
</template>

<script>
import { startRegistration } from '@simplewebauthn/browser';

export default {
  name: "UserLoginDevicesCard",
  data() {
    return {
      registering: false,
      registrationError: false,
      registrationErrorMessage: '',
      loadingCredentials: true,
      errorLoadingCredentials: false,
      errorDeletingCredential: false,
      credentialDeletionErrorMessage: '',
      credentials: []
    };
  },
  methods: {
    loadCredentials() {
      this.loadingCredentials = true;
      this.errorLoadingCredentials = false;

      return this
        .$axios
        .get('/api/users/me/credentials')
        .then((credentials) => {
          this.credentials = credentials;
          this.loadingCredentials = false;
        })
        .catch((error) => {
          this.loadingCredentials = false;
          this.errorLoadingCredentials = true;
        });
    },
    onAddDevice() {
      this.registering = true;
      this.registrationError = false;
      this.registrationErrorMessage = '';

      return this
        .$axios
        //retrieve registration options/challenge first
        .$get('/api/webauthn/register')
        //then start attestation ceremony
        .then((createOptions) => startRegistration(createOptions))
        //then try to register credential in the authenticator response
        .then((attestationResponse) => {
          return this
            .$axios
            .$post('/api/webauthn/register', attestationResponse);
        })
        //then update the device list if successful registration
        .then((createResult) => {
           this.loadCredentials();
           return true;    
        })
        .catch((error) => {
          this.registering = false;
          this.registrationError = true;
          this.registrationErrorMessage = error;
        });
    },
    onDeleteCredential(credential) {
      this.loadingCredentials = true;
      this.errorDeletingCredential = false;

      return this
        .$axios
        .delete(`/api/users/me/credentials/${credential.id}`)
        .then(() => {
          return this.loadCredentials();
        })
        .catch((error) => {
          this.errorDeletingCredential = true;
          this.credentialDeletionErrorMessage = error;
        });
    }
  },
  async beforeMount() {
    this.loadCredentials();
  }
};
</script>