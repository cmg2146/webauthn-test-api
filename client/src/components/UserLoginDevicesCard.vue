<template>
  <v-card
    class="pa-0"
  >
    <v-card-title class="justify-center px-10 py-5">
      Login Devices
    </v-card-title>

    <v-divider class="mb-5"></v-divider>

    <template v-if="errorLoadingCredentials">
      <div class="text-center py-10">
        There was an error loading your device list
      </div>
    </template>

    <template v-else-if="!credentials.length">
      <div class="text-center py-10">
        You don't have any devices to log in with.
        Click below to add a device.
      </div>
    </template>

    <template v-else>
      <v-card-text
        v-for="credential in credentials"
        :key="credential.id"
        class="px-10 py-5"
      >
        <v-list-item two-line>
          <v-list-item-icon>
            <v-icon>{{ getIconNameForCredential(credential) }}</v-icon>
          </v-list-item-icon>

          <v-list-item-content>
            <v-list-item-title>{{ credential.displayName }}</v-list-item-title>
            <v-list-item-subtitle
              v-if="activeCredentialId === credential.id"
              class="mt-2"
            >
              <v-chip color="primary" small>
                Logged in
              </v-chip>
            </v-list-item-subtitle>
          </v-list-item-content>

          <v-list-item-action>
            <v-menu
              bottom
              left
            >
              <template v-slot:activator="{ on, attrs }">
                <v-btn
                  icon
                  v-bind="attrs"
                  v-on="on"
                >
                  <v-icon>mdi-dots-vertical</v-icon>
                </v-btn>
              </template>

              <v-list>
                <v-dialog
                  v-model="confirmingDeleteCredential"
                  max-width="350"
                >
                  <template v-slot:activator="{ on, attrs }">
                    <v-list-item
                      v-bind="attrs"
                      v-on="on"
                    >
                      <v-list-item-icon color="error">
                        <v-icon>mdi-delete</v-icon>
                      </v-list-item-icon>
                      <v-list-item-title>
                        Delete
                      </v-list-item-title>
                    </v-list-item>
                  </template>
                  <v-card>
                    <v-card-title class="text-h5">
                      Confirm Deletion
                    </v-card-title>
                    <v-card-text>
                      Are you sure you want to delete this device?
                    </v-card-text>
                    <v-card-actions>
                      <v-spacer></v-spacer>
                      <v-btn
                        text
                        @click="confirmingDeleteCredential = false"
                      >
                        No
                      </v-btn>                      
                      <v-btn
                        color="primary"
                        text
                        @click="onDeleteCredential(credential)"
                      >
                        Yes
                      </v-btn>
                    </v-card-actions>
                  </v-card>
                </v-dialog>
              </v-list>
            </v-menu>
          </v-list-item-action>          
        </v-list-item>

        <v-fade-transition>
          <v-overlay
            absolute
            :value="loadingCredentials"
          >
            <v-progress-circular
              indeterminate
              color="primary"
            ></v-progress-circular>
          </v-overlay>
        </v-fade-transition>
      </v-card-text>
    </template>

    <v-divider class="mt-5"></v-divider>

    <v-card-actions class="d-flex justify-center px-10 py-5">
      <v-btn
        text
        block
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
      credentials: [],
      activeCredentialId: null,
      confirmingDeleteCredential: false
    };
  },
  methods: {
    loadCredentials() {
      this.loadingCredentials = true;
      this.errorLoadingCredentials = false;

      return this
        .$axios
        .$get('/api/users/me/credentials')
        .then((credentials) => {
          this.credentials = credentials;
          this.loadingCredentials = false;
        })
        .catch((error) => {
          this.loadingCredentials = false;
          this.errorLoadingCredentials = true;
        });
    },
    loadActiveCredential() {
      return this
        .$axios
        .$get('/api/webauthn/active-credential')
        .then((credential) => {
          this.activeCredentialId = credential.id;
        })
        .catch(() => {});
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
        //then try to register credential from the authenticator response
        .then((attestationResponse) => {
          return this
            .$axios
            .$post('/api/webauthn/register', attestationResponse);
        })
        //then update the device list if successful registration
        .then((createResult) => {
          this.registering = false;
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
      this.confirmingDeleteCredential = false;

      return this
        .$axios
        .$delete(`/api/users/me/credentials/${credential.id}`)
        .then(() => {
          this.loadCredentials();
          return true;
        })
        .catch((error) => {
          this.loadingCredentials = false;
          this.errorDeletingCredential = true;
          this.credentialDeletionErrorMessage = error;
        });
    },
    getIconNameForCredential(credential) {
      var attFmt = credential.attestationFormatId;

      if (attFmt === 'tpm') {
        return 'mdi-laptop';
      } else if (attFmt === 'android-key'
        || attFmt === 'android-safetynet'
        || attFmt === 'apple'
      ) {
        return 'mdi-cellphone';
      }

      return 'mdi-usb-flash-drive';
    }
  },
  async mounted() {
    this.loadCredentials();
    this.loadActiveCredential();
  }
};
</script>