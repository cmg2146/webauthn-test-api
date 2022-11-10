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
          Create an account for WebAuthn-Test
        </h2>        
      </v-col>
    </v-row>
    <v-row justify="center" align="start">
      <v-col cols="12" md="6" lg="5" class="d-flex flex-column align-stretch">
        <v-card
          class="pa-5"
        >
          <v-form
            ref="registerForm"
            @submit.prevent="onSubmitRegistration"
          >
            <v-card-text>
              <v-text-field
                v-model="displayName"
                :rules="[nameRules.required, nameRules.max]"
                name="displayName"
                label="Username/Account Name"
                counter="255"
                class="mb-3"      
              ></v-text-field>
              <v-text-field
                v-model="firstName"
                :rules="[nameRules.required, nameRules.max]"
                name="displayName"
                label="First Name"
                counter="255"
                class="mb-3"      
              ></v-text-field>
              <v-text-field
                v-model="lastName"
                :rules="[nameRules.required, nameRules.max]"
                name="displayName"
                label="Last Name"
                counter="255"
                class="mb-3"      
              ></v-text-field>
            </v-card-text>
            <v-card-text>
              <v-btn
                block
                color="primary"
                :loading="registering"
                type="submit"
              >Register</v-btn>
            </v-card-text>            
          </v-form>       
        </v-card>
      </v-col>
    </v-row>
    <v-row justify="center" align="start">
      <v-col cols="12" md="6" lg="5" class="d-flex flex-column align-stretch">
        <v-alert
          v-model="registrationError"
          color="error"
          class="pa-5"
        >
          {{registrataionErrorMessage}}  
        </v-alert>
      </v-col>
    </v-row>    
  </v-container>
</template>

<script>
export default {
  name: 'RegisterPage',
  auth: 'guest',
  data () {
    return {
      registering: false,
      registrationError: false,
      registrataionErrorMessage: '',
      displayName: '',
      firstName: '',
      lastName: '',
      nameRules: {
        required: value => !!value || 'Required.',
        max: v => v.length <= 255 || 'Max 255 characters'
      }
    };
  },
  methods: {
    onSubmitRegistration() {
      if (this.$refs.registerForm.validate())
      {
        this.registering = true;
        this.registrationError = false;

        var user = {
          displayName: this.displayName,
          firstName: this.firstName,
          lastName: this.lastName
        };

        return this
          .$auth
          .loginWith(
            'cookie', {
              url: '/api/webauthn/register-user',
              method: 'post',
              data: user
            }
          )
          .catch((error) => {
            this.registering = false;
            this.registrationError = true;
            this.registrataionErrorMessage = error;
          });
      }
    }
  }
};
</script>