
#include <24FV16KM202.h>
#DEVICE ADC=8
#device ICD=3

#use delay(clock = 32MHZ, internal = 8MHZ)


#include <math.h>
#FUSES FRC_PLL
#FUSES NOWDT                    //No Watch Dog Timer
#FUSES CKSFSM                   //Clock Switching is enabled, fail Safe clock monitor is enabled
#FUSES NOBROWNOUT               //No brownout reset
#FUSES BORV_LOW                 //Brown-out Reset set to lowest voltage

#use fast_io(B)

#define LCD_ENABLE_PIN  PIN_A7
#define LCD_RS_PIN      PIN_B8
#define LCD_RW_PIN      PIN_B9
#define LCD_DATA4       PIN_B12
#define LCD_DATA5       PIN_B13
#define LCD_DATA6       PIN_B14
#define LCD_DATA7       PIN_B15

#include <LCD.C>



#USE RS232(UART2, BAUD = 115200, PARITY = N, BITS = 8, STOP = 1, TIMEOUT = 500))

#define COEF_LENGTH 64
#define BUFFER_SIZE 300

/*-------------------------------------------------------------------------------------------------------------------*/
// LPF Filter coefficients array values in fixed-point notation Q15
int fir_coef[COEF_LENGTH] ;

int input_samples[COEF_LENGTH]; // array used as a circular buffer for the input samples
unsigned int8 coef_index = 0; // used as the index for the filter coefficients array in the difference equation calculation
unsigned int8 input_index = 0; // used as the index for the input samples array in the difference equation calculation
unsigned int8 cur = 0; // keeps track of the curent position of the circular buffer

long long accumulator = 0; // accumulator of the output value in the difference equation calculation
unsigned int16 start, end; // used to calculate the sampling frequency Fs

int out; // holds the current output value
/*-------------------------------------------------------------------------------------------------------------------*/


signed int16 input_buffer[BUFFER_SIZE]; //input buffer to store values recieved from GUI


/*-------------------------------------------------------------------------------------------------------------------*/
                                                      //global Variables for UART
int1 serial_flag = 0;      //signals a character is received
char ch;                   //variable to store character received from serial 
int32 UART_receive_buffer_index=0;  //index variable for UART value received buffer 
int1 coefficient_received_done=0;   //signal that represents coefficient received from UART
int1 offset_value_received_done=0;  //signal that represents offset value received
int1 shift_value_received_done=0;   //signal that represents shift value received
int temp_value=0;                   //variable used to compute numbers
int1 restart_flag=0;                //signal that represent restart
int1 stop_flag=0;                     //signal that represents stop
int1 sampling_freq_request_flag=0;     //signal that represents sampling frequency request from GUI
int1 plot_request_flag=0;        //signal that represents plot request from GUI
int1 real_time_plot_flag=0;      //signal that represent real time plot request from GUI
/*-------------------------------------------------------------------------------------------------------------------*/
                                                        //global variables for Signal Processing
unsigned int16 offset_value=32;     //variable that hollds value for offset
unsigned int16 shift_value=24;      //variable that holds value for shift
/*-------------------------------------------------------------------------------------------------------------------*/

//interrupt handler for UART
#INT_RDA2
void isr_uart()
{
   ch=getc();
   if(ch=='{')
      UART_receive_buffer_index=0;
   else if(ch=='}')
      coefficient_received_done=1;
   else if(ch=='-' || ch==' ' || (ch>='0' && ch<='9'))
   {
      input_buffer[UART_receive_buffer_index++] = ch;   
      if(ch>='0' && ch<='9')
         temp_value=(temp_value*10)+ch-48;      //converting character into number
   }
   else if(ch=='~')     //signal that represent incoming new value
   {
      temp_value=0;
      UART_receive_buffer_index=0;
   }
   else if(ch=='!')     //singal that represents receiving of offset value
   {
      offset_value_received_done=1;
      offset_value=temp_value;
   }
   else if(ch=='&')     //singal that represents incoming of new value
   {
      temp_value=0;
      UART_receive_buffer_index=0;
   }
   else if(ch=='$')     //signal that represents shift value received
   {
      shift_value_received_done=1;
      shift_value=temp_value;
   }
   else if(ch=='%')     //signal that represents restart request
   {  
      temp_value=0;
      coefficient_received_done=0;
      offset_value_received_done=0;
      shift_value_received_done=0;
      restart_flag=1;
   }
   else if(ch=='^')  //signals PIC to stop sampling
   {
      stop_flag=1;
   }
   else if(ch=='#')  //signals PIC to start
   {
      stop_flag=0;
   }
   else if(ch=='@')     //signals PIC to send sampling frequency
   {
      sampling_freq_request_flag=1;
   }
   else if(ch=='*')  //signals PIC to send data to GUI to plot
   {
      plot_request_flag=1;
   }
   else if(ch=='/')  //signals PIC to send data continuosly for real time plot
   {
      real_time_plot_flag=1;
   }
   else if(ch=='.')  //signals PIC to stop sending data continuosly 
   {
      real_time_plot_flag=0;
   }
   serial_flag=1;
}

void main()
{  
   /*-------------------------------------------------------------------------------------------------------------------*/
   //start of setup and initializations
   lcd_init();    //initializing lcd
   
   
   // Setup ADC
   setup_adc(ADC_CLOCK_DIV_2 | ADC_TAD_MUL_4);  
   setup_adc_ports(sAN0 | VSS_VDD);
   // Setup DAC
   setup_dac(1,DAC_REF_VDD | DAC_ON);
   //setup opamp to amplify the output signal from DAC
   setup_opamp1(OPAMP_ENABLED | OPAMP_PI_TO_DAC | OPAMP_NI_TO_OUTPUT | OPAMP_HIGH_POWER_MODE);   
   // Setup Timer to calculate sampling frequency (Fs)
   setup_timer1(T1_INTERNAL | T1_DIV_BY_1);
   set_timer1(0);
   
   enable_interrupts(INTR_GLOBAL);
   enable_interrupts(INT_RDA2); 
   //end of setup and initializations
   /*-------------------------------------------------------------------------------------------------------------------*/
   //start of program
   printf(lcd_putc,"\fSushan ECE 422");
   lcd_gotoxy(1,2);
   printf(lcd_putc,"FIR Filter");
   
   printf("+Sushan ECE 422\nFIR Filter=");
   /*-------------------------------------------------------------------------------------------------------------------*/
   //local variables
   int1 negative_sign=0;
   signed int16 temp_coeff_value=0;
   int16 temp_coeff_index=0;  //index for bytes received in UART
   int8 coeff_index=0;
   
   unsigned int8 raw_samples_index=0;
   unsigned int8 processed_samples_index=0;
   
   signed int8 output_processed_samples_buffer[100];      //output buffer that holds the processed value need to be sent to GUI
   
   //int8 sending_ch=0;
   signed int8  output_raw_samples_buffer[100];             // output buffer that holds raw samples need to be sent to GUI
   
   int real_time_plot_count=0;
   /*-------------------------------------------------------------------------------------------------------------------*/ 
   start_loading_coefficient:
   while(coefficient_received_done==0)
   {
      //wait here until we recieve all the coefficients from GUI
   }
   
   temp_coeff_index=0;     //reinitializing temp_coeff_index to store coefficients
   coeff_index=0;          //reinitializing coeff_index to restore coefficient values
   while(coefficient_received_done==1)
   {
      if(input_buffer[temp_coeff_index]=='-')
      {
         negative_sign=1;
         temp_coeff_index+=1;
      }
      else if(input_buffer[temp_coeff_index]>='0' && input_buffer[temp_coeff_index]<='9')
      {
         temp_coeff_value= (temp_coeff_value*10) + (input_buffer[temp_coeff_index]-48);
         temp_coeff_index +=1;   //increasing the index
      }
      else if((input_buffer[temp_coeff_index]==' '))
      {
         //store the coeff into the buffer
         if(negative_sign==1)
         {
            temp_coeff_value=0-temp_coeff_value;
            fir_coef[coeff_index++]=(temp_coeff_value);
         }
        
         else
            fir_coef[coeff_index++]=temp_coeff_value;
         temp_coeff_value=0;
         negative_sign=0;
         temp_coeff_index +=1;
         if(coeff_index>63)
            coefficient_received_done=0;
      }
   }
  
   /*-------------------------------------------------------------------------------------------------------------------*/
      
   stop_loading_coefficient:
   /*-------------------------------------------------------------------------------------------------------------------*/
   
   while(offset_value_received_done==0)
   {
      //wait here until we recieve offset value
   }
   
   
   while(shift_value_received_done==0)
   {
      //wait here until we recieve offset value
   }
   
   /*-------------------------------------------------------------------------------------------------------------------*/
   stopping_point:
   
   while(stop_flag==1)
   {
      //wait here until we recieve stop flag
   }
   
   // Initialize the input samples array with zeros
   for(int i = COEF_LENGTH; i < 1; i--)
   {
      input_samples[i] =  0;
   }
   restart_flag=0; 
   
   /*-------------------------------------------------------------------------------------------------------------------*/
   
   while(1)
   {
      start = get_timer1();
      
      if(restart_flag==1)
      {
         restart_flag=0;
         goto start_loading_coefficient;
      }
      else if(stop_flag==1)
      {
         goto stopping_point;
      }
      else if(plot_request_flag==1)
      {
         printf("(");   //sending signal for incoming processed samples
         for(int i=0; i<100; i++)
         {
            printf("%i",output_processed_samples_buffer[i]);
            printf(" ");
         }
         
         printf(")");  //sending signal for incoming raw samples;
         for(int i=0; i<100; i++)
         {
            printf("%i",output_raw_samples_buffer[i]);
            printf(" ");
         }
         plot_request_flag=0;
      }
      else if(real_time_plot_flag==1 && real_time_plot_count==100)
      {
         printf("(");   //sending signal for incoming processed samples
         for(int i=0; i<100; i++)
         {
            printf("%i",output_processed_samples_buffer[i]);
            printf(" ");
         }
         
         printf(")");  //sending signal for incoming raw samples;
         for(int i=0; i<100; i++)
         {
            printf("%i",output_raw_samples_buffer[i]);
            printf(" ");
         }
      }
      
      real_time_plot_count++;
      if(real_time_plot_count>100)
         real_time_plot_count=0;
         
      output_raw_samples_buffer[raw_samples_index++]=read_adc(ADC_START_AND_READ);  //storing raw samples in output buffer to send to GUI
      if(raw_samples_index>100)
         raw_samples_index=0;    //reseting the index of output buffer
         
      input_samples[cur] =  read_adc(ADC_START_AND_READ)<<8;   //read current sample from ADC and store in circular buffer
      
      input_index = cur;
      accumulator = 0;
      coef_index = 0;
      while(coef_index < COEF_LENGTH - 1)
      {
         if(restart_flag==1)
         {
            restart_flag=0;
            goto start_loading_coefficient;     //going back to load coefficients 
         }
         if(stop_flag==1)
         {
            goto stopping_point;
         }
         accumulator +=(long long) input_samples[input_index]*fir_coef[coef_index];
         // condition for the circular buffer
         if(input_index == COEF_LENGTH - 1)
            input_index = 0;
         else
            input_index++;
            
         coef_index++;
      }
      
      accumulator=(long long)accumulator>>shift_value;
      out=accumulator+offset_value;
      
      dac_write(1,out);
      
      output_processed_samples_buffer[processed_samples_index++]=out;
      if(processed_samples_index>100)
         processed_samples_index=0;    //reseting the index of output buffer
         
      // condition for the circular buffer
      if(cur == 0)
         cur = COEF_LENGTH - 1;
      else
         cur--;
   
      end = get_timer1();
      if(sampling_freq_request_flag==1)
      {
         printf("@%.0f}",(1.0/((float)(end-start)))*1.6*pow(10,7));     //converting time value into frequency
         sampling_freq_request_flag=0;
      }
      
   }
}

